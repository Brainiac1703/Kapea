using Kapea.Application.Abstractions;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Import;

/// <summary>Se ha pedido importar sobre una cuenta que no existe o que es de otra plataforma.</summary>
public sealed class ImportTargetException(string message) : InvalidOperationException(message);

/// <summary>
/// Motor de importación. Es el mismo para todos los orígenes: los adaptadores entregan
/// registros normalizados y a partir de aquí ya no importa de dónde vinieron.
/// </summary>
/// <remarks>
/// Toda importación pasa por una fase de staging donde los registros quedan
/// clasificados sin escribir nada en las tablas de dominio. XTB expone esa fase como
/// vista previa; los adaptadores de API la confirman en el acto. Un solo camino, dos
/// formas de confirmarlo.
/// </remarks>
public sealed class ImportPipeline(
    IImportRepository repository,
    IAssetCatalog assetCatalog,
    IExchangeRateProvider exchangeRates,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ImportPipeline> logger)
{
    /// <summary>
    /// Normaliza y clasifica lo que trae el adaptador y lo deja preparado, sin tocar
    /// las tablas de dominio.
    /// </summary>
    public async Task<ImportRun> StageAsync(
        Guid accountId,
        ImportReadResult read,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        var account = await RequireAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        var run = ImportRun.Start(currentUser.Id, accountId, account.Platform, timeProvider.GetUtcNow(), fileName);

        // Se indexa por posición y no por registro: ImportRecord tiene igualdad por
        // valor, así que dos registros idénticos del mismo lote colapsarían en uno solo
        // al usarlos como clave, que es justo el caso que hay que poder distinguir.
        var fingerprints = read.Records
            .Select(record => ImportFingerprint.For(accountId, account.Platform, record))
            .ToList();

        var existing = await repository
            .FindExistingFingerprintsAsync(accountId, [.. fingerprints.Distinct()], cancellationToken)
            .ConfigureAwait(false);

        // El lote puede traer dos veces el mismo registro; sin este control la
        // comprobación contra la base de datos no lo vería, porque aún no está escrito.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < read.Records.Count; index++)
        {
            var record = read.Records[index];
            var fingerprint = fingerprints[index];
            var duplicated = existing.Contains(fingerprint) || !seen.Add(fingerprint);

            run.Stage(new StagedRecord(
                run.Id,
                duplicated ? StagedRecordOutcome.Duplicate : StagedRecordOutcome.Importable,
                fingerprint,
                record.NaturalId,
                record.RowNumber,
                record.RawContent,
                StagedPayload.Serialize(record),
                rejectionReason: null));
        }

        foreach (var rejected in read.Rejected)
        {
            run.Stage(new StagedRecord(
                run.Id,
                StagedRecordOutcome.Rejected,
                $"rejected:{run.Id:N}:{rejected.RowNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? rejected.NaturalId}",
                rejected.NaturalId,
                rejected.RowNumber,
                rejected.RawContent,
                payload: string.Empty,
                rejected.Reason));
        }

        run.CountNonFinancial(read.NonFinancialRecordCount);

        await repository.AddRunAsync(run, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Importación {Ejecucion} preparada: {Leidos} leídos, {Importables} importables, {Duplicados} duplicados, {Rechazados} rechazados.",
            run.Id, run.RecordsRead, run.RecordsImported, run.DuplicatesDiscarded, run.RecordsRejected);

        return run;
    }

    /// <summary>
    /// Materializa los registros importables. Es atómico: o entran todos o no entra
    /// ninguno, y un fallo deja la ejecución fallida sin movimientos sueltos.
    /// </summary>
    public async Task<ImportRun> ConfirmAsync(
        Guid runId,
        DateTimeOffset? coversUntil = null,
        CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken).ConfigureAwait(false);

        try
        {
            await repository.ExecuteInTransactionAsync(async token =>
            {
                var transactions = new List<Transaction>();

                foreach (var staged in run.Records.Where(record => record.Outcome == StagedRecordOutcome.Importable))
                {
                    transactions.Add(await MaterialiseAsync(run, staged, token).ConfigureAwait(false));
                }

                await repository.AddTransactionsAsync(transactions, token).ConfigureAwait(false);

                run.Complete(timeProvider.GetUtcNow(), coversUntil);

                await repository.SaveChangesAsync(token).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "La importación {Ejecucion} ha fallado al confirmar.", run.Id);

            run.Fail(exception.Message, timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            throw;
        }

        logger.LogInformation("Importación {Ejecucion} confirmada con {Importados} movimientos.", run.Id, run.RecordsImported);

        return run;
    }

    /// <summary>Abandona una vista previa. No deja movimientos ni ejecución completada.</summary>
    public async Task DiscardAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await RequireRunAsync(runId, cancellationToken).ConfigureAwait(false);

        run.Discard(timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Vuelve a intentar los registros rechazados de una ejecución con los datos ya
    /// almacenados. Los que siguen sin poder normalizarse conservan su rechazo con el
    /// motivo actualizado.
    /// </summary>
    public async Task<ImportRun> ReprocessRejectedAsync(
        Guid runId,
        Func<StagedRecord, ImportRecord?> reinterpret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reinterpret);

        var run = await RequireRunAsync(runId, cancellationToken).ConfigureAwait(false);
        var account = await RequireAccountAsync(run.AccountId, cancellationToken).ConfigureAwait(false);

        foreach (var staged in run.Records.Where(record => record.Outcome == StagedRecordOutcome.Rejected).ToList())
        {
            ImportRecord? reinterpreted;

            try
            {
                reinterpreted = reinterpret(staged);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                staged.Reclassify(StagedRecordOutcome.Rejected, exception.Message);

                continue;
            }

            if (reinterpreted is null)
            {
                staged.Reclassify(StagedRecordOutcome.Rejected, staged.RejectionReason ?? "Sigue sin poder interpretarse.");

                continue;
            }

            var fingerprint = ImportFingerprint.For(run.AccountId, account.Platform, reinterpreted);
            var existing = await repository
                .FindExistingFingerprintsAsync(run.AccountId, [fingerprint], cancellationToken)
                .ConfigureAwait(false);

            staged.Reclassify(existing.Contains(fingerprint)
                ? StagedRecordOutcome.Duplicate
                : StagedRecordOutcome.Importable);
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return run;
    }

    /// <summary>
    /// Elimina una ejecución y los movimientos que creó. Se bloquea si alguno participa
    /// en un traspaso confirmado o en un ajuste que dependa de él: borrarlo dejaría
    /// cifras apoyadas en datos que ya no existen.
    /// </summary>
    public async Task DeleteAsync(
        Guid runId,
        Func<IReadOnlyList<Transaction>, IReadOnlyList<string>> findDependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(findDependencies);

        var run = await RequireRunAsync(runId, cancellationToken, allowAnyStatus: true).ConfigureAwait(false);
        var transactions = await repository.ListTransactionsOfRunAsync(runId, cancellationToken).ConfigureAwait(false);
        var dependencies = findDependencies(transactions);

        if (dependencies.Count > 0)
        {
            throw new DomainException(
                "No se puede eliminar la importación: " + string.Join("; ", dependencies) + ".");
        }

        await repository.ExecuteInTransactionAsync(async token =>
        {
            await repository.RemoveTransactionsAsync(transactions, token).ConfigureAwait(false);
            await repository.RemoveRunAsync(run, token).ConfigureAwait(false);
            await repository.SaveChangesAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Importación {Ejecucion} eliminada con sus {Movimientos} movimientos.", runId, transactions.Count);
    }

    private async Task<Transaction> MaterialiseAsync(ImportRun run, StagedRecord staged, CancellationToken cancellationToken)
    {
        var record = StagedPayload.Deserialize(staged.Payload, staged.RawContent);
        var occurredAt = record.ToOccurrence();

        var assetId = record.AssetSymbol is { Length: > 0 } symbol
            ? (await assetCatalog
                .ResolveAsync(symbol, record.AssetClass ?? Domain.Assets.AssetClass.Crypto, cancellationToken)
                .ConfigureAwait(false)).Id
            : (Guid?)null;

        var rate = await ResolveRateAsync(record, occurredAt, cancellationToken).ConfigureAwait(false);

        return Transaction.Imported(
            run.UserId,
            run.AccountId,
            record.Type,
            assetId,
            new Quantity(record.Quantity),
            record.UnitPrice is { } price ? new Money(price, record.Currency) : null,
            new Money(record.GrossAmount, record.Currency),
            new Money(record.Fee, record.Currency),
            occurredAt,
            TransactionSource.FromImport(run.Id, record.NaturalId, record.RowNumber, staged.Fingerprint, staged.RawContent),
            record.Withholding is { } withholding ? new Money(withholding, record.Currency) : null,
            rate);
    }

    /// <summary>
    /// Congela el tipo de cambio en el movimiento. Se resuelve una sola vez, al
    /// importar: a partir de ahí ningún recálculo vuelve a consultar la fuente.
    /// </summary>
    private async Task<ExchangeRate?> ResolveRateAsync(
        ImportRecord record,
        Occurrence occurredAt,
        CancellationToken cancellationToken)
    {
        if (record.Currency.IsEuro)
        {
            return null;
        }

        var date = DateOnly.FromDateTime(occurredAt.InSourceTimeZone.Date);
        var rate = await exchangeRates.ResolveAsync(record.Currency, date, cancellationToken).ConfigureAwait(false);

        return rate ?? throw new DomainException(
            $"No hay tipo de cambio de {record.Currency.Code} para el {date:yyyy-MM-dd}: ingesta los tipos antes de importar.");
    }

    private async Task<PlatformAccount> RequireAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await repository.FindAccountAsync(accountId, cancellationToken).ConfigureAwait(false)
        ?? throw new ImportTargetException($"La cuenta {accountId} no existe.");

    private async Task<ImportRun> RequireRunAsync(
        Guid runId,
        CancellationToken cancellationToken,
        bool allowAnyStatus = false)
    {
        var run = await repository.FindRunAsync(runId, cancellationToken).ConfigureAwait(false)
            ?? throw new ImportTargetException($"La importación {runId} no existe.");

        if (!allowAnyStatus && run.Status != ImportRunStatus.Staged)
        {
            throw new DomainException($"La importación {runId} ya está {run.Status}.");
        }

        return run;
    }
}
