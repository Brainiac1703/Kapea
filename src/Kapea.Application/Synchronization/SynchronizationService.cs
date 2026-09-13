using Kapea.Application.Abstractions;
using Kapea.Application.Credentials;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Synchronization;

/// <summary>Cómo terminó la sincronización de una cuenta.</summary>
public enum SynchronizationOutcome
{
    Imported = 1,

    /// <summary>Ya había otra sincronización en curso para esa cuenta.</summary>
    SkippedOverlapping = 2,

    /// <summary>La plataforma rechazó la credencial: queda marcada como inválida.</summary>
    CredentialInvalid = 3,

    /// <summary>Falló por otra causa. Las demás plataformas siguen su curso.</summary>
    Failed = 4,
}

/// <param name="Owner">De quién es la cuenta. Hace falta para rehacer su cartera después.</param>
public sealed record AccountSynchronizationResult(
    Guid AccountId,
    PlatformCode Platform,
    SynchronizationOutcome Outcome,
    int ImportedRecords,
    string? Detail,
    Domain.ValueObjects.UserId Owner = default);

public sealed record SynchronizationReport(IReadOnlyList<AccountSynchronizationResult> Results)
{
    public int ImportedAccounts => Results.Count(result => result.Outcome == SynchronizationOutcome.Imported);

    public int FailedAccounts => Results.Count(result =>
        result.Outcome is SynchronizationOutcome.Failed or SynchronizationOutcome.CredentialInvalid);
}

/// <summary>
/// Sincronización periódica de las cuentas con adaptador de API.
/// </summary>
/// <remarks>
/// Se ejecuta siempre desde el servidor: el cliente WebAssembly no ve las credenciales
/// ni contacta con ninguna plataforma. Cada cuenta se importa desde el instante de su
/// última importación correcta, y el fallo de una no puede impedir el resto.
/// </remarks>
public sealed class SynchronizationService(
    ISynchronizationRepository repository,
    IImportRepository importRepository,
    IImportAdapterRegistry adapters,
    ImportPipeline pipeline,
    BrokerCredentialService credentials,
    Portfolio.InternalTransferService transfers,
    Portfolio.PortfolioCalculationService calculation,
    IAccountSyncLock accountLock,
    ICurrentUserScope currentUserScope,
    TimeProvider timeProvider,
    ILogger<SynchronizationService> logger)
{
    /// <summary>Duración del cerrojo. Amplia frente a una carga inicial larga, pero acotada frente a un cuelgue.</summary>
    public static readonly TimeSpan LockLease = TimeSpan.FromHours(2);

    /// <summary>
    /// Punto desde el que arranca la primera importación de una cuenta. Kapea no existía
    /// antes; pedir más atrás solo alarga la carga inicial sin traer nada.
    /// </summary>
    public static readonly DateTimeOffset EarliestHistory = new(2010, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <param name="owner">
    /// Limita la sincronización a las cuentas de una persona. Lo usa quien la lanza a
    /// mano desde la aplicación: pedirla no puede servir para mover los datos de otro.
    /// Nulo en la ejecución programada, que recorre todas.
    /// </param>
    /// <param name="fromTheBeginning">
    /// Relee el histórico entero en lugar de pedir solo lo nuevo. Hace falta cuando se
    /// corrige cómo se interpreta un movimiento: lo ya importado se descarta por
    /// duplicado, así que solo entra lo que antes no se sabía leer.
    /// </param>
    public async Task<SynchronizationReport> RunAsync(
        Domain.ValueObjects.UserId? owner = null,
        bool fromTheBeginning = false,
        CancellationToken cancellationToken = default)
    {
        var all = await repository.ListTargetsAsync(cancellationToken).ConfigureAwait(false);

        var targets = owner is { } only
            ? [.. all.Where(candidate => candidate.Account.UserId == only)]
            : all;
        var results = new List<AccountSynchronizationResult>();

        logger.LogInformation("Sincronización iniciada para {Cuentas} cuentas.", targets.Count);

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(await SynchroniseAsync(target, fromTheBeginning, cancellationToken).ConfigureAwait(false));
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Importar no basta: la cartera y los resultados son proyecciones de los
        // movimientos, y sin recalcularlas la sincronización deja las cifras como
        // estaban. Se hace por usuario, porque el cálculo mira solo lo suyo.
        foreach (var affected in results
            .Where(result => result.Outcome == SynchronizationOutcome.Imported && result.ImportedRecords > 0)
            .Select(result => result.Owner)
            .Distinct())
        {
            await RefreshAsync(affected, cancellationToken).ConfigureAwait(false);
        }

        var report = new SynchronizationReport(results);

        logger.LogInformation(
            "Sincronización terminada: {Correctas} cuentas importadas y {Fallidas} con fallo.",
            report.ImportedAccounts, report.FailedAccounts);

        return report;
    }

    /// <summary>
    /// Rehace la cartera y los resultados de un usuario tras importarle movimientos.
    /// </summary>
    /// <remarks>
    /// Los traspasos se buscan antes del cálculo: mover una cripto de una cuenta a otra
    /// no es una venta seguida de una compra, y contarlo así inventaría un resultado que
    /// no existe. Un fallo aquí no invalida lo importado, que ya está guardado.
    /// </remarks>
    private async Task RefreshAsync(Domain.ValueObjects.UserId owner, CancellationToken cancellationToken)
    {
        currentUserScope.ActAs(owner);

        try
        {
            await transfers.ProposeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await calculation.RecalculateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception, "No se ha podido rehacer la cartera del usuario {Usuario} tras sincronizar.", owner.Value);
        }
    }

    private async Task<AccountSynchronizationResult> SynchroniseAsync(
        SynchronizationTarget target,
        bool fromTheBeginning,
        CancellationToken cancellationToken)
    {
        var account = target.Account;

        // Se declara el dueño de la cuenta antes de tocar nada suyo: lo que se importe
        // tiene que quedar a su nombre, y el filtro global de la base de datos se apoya
        // en esto para no mezclar los datos de dos usuarios.
        currentUserScope.ActAs(account.UserId);

        await using var lease = await accountLock
            .TryAcquireAsync(account.Id, LockLease, cancellationToken).ConfigureAwait(false);

        if (lease is null)
        {
            logger.LogWarning(
                "Sincronización de la cuenta {Cuenta} omitida: ya hay otra en curso.", account.Id);

            return new AccountSynchronizationResult(
                account.Id, account.Platform, SynchronizationOutcome.SkippedOverlapping, 0,
                "Ya había una sincronización en curso.", account.UserId);
        }

        try
        {
            return await ImportAsync(target, fromTheBeginning, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Un fallo se queda en su cuenta: las demás plataformas siguen sincronizando.
            logger.LogError(
                exception, "La sincronización de la cuenta {Cuenta} ha fallado.", account.Id);

            if (IsInvalidCredential(exception))
            {
                target.Credential.MarkInvalid(exception.Message);

                return new AccountSynchronizationResult(
                    account.Id, account.Platform, SynchronizationOutcome.CredentialInvalid, 0,
                    exception.Message, account.UserId);
            }

            return new AccountSynchronizationResult(
                account.Id, account.Platform, SynchronizationOutcome.Failed, 0, exception.Message, account.UserId);
        }
    }

    private async Task<AccountSynchronizationResult> ImportAsync(
        SynchronizationTarget target,
        bool fromTheBeginning,
        CancellationToken cancellationToken)
    {
        var account = target.Account;
        var secret = await credentials.ResolveAsync(target.Credential, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se ha podido recuperar el secreto de la cuenta {account.Id}.");

        // Se pide solo lo posterior a la última importación correcta. Una fallida no
        // mueve ese punto, así que lo que no llegó a entrar se vuelve a pedir.
        var from = fromTheBeginning
            ? EarliestHistory
            : await importRepository
                .FindLastSuccessfulImportInstantAsync(account.Id, cancellationToken).ConfigureAwait(false)
                ?? EarliestHistory;

        var to = timeProvider.GetUtcNow();
        var adapter = adapters.GetApiAdapter(account.Platform);

        logger.LogInformation(
            "Sincronizando la cuenta {Cuenta} de {Plataforma} desde {Desde}.", account.Id, account.Platform, from);

        var read = await adapter
            .ReadAsync(new ApiCredential(secret.Key, secret.Secret), from, to, cancellationToken)
            .ConfigureAwait(false);

        var run = await pipeline.StageAsync(account.Id, read, fileName: null, cancellationToken).ConfigureAwait(false);
        var confirmed = await pipeline.ConfirmAsync(run.Id, to, cancellationToken).ConfigureAwait(false);

        target.Credential.MarkSynchronized(to);

        logger.LogInformation(
            "Cuenta {Cuenta} sincronizada: {Importados} movimientos nuevos y {Duplicados} duplicados descartados.",
            account.Id, confirmed.RecordsImported, confirmed.DuplicatesDiscarded);

        return new AccountSynchronizationResult(
            account.Id, account.Platform, SynchronizationOutcome.Imported, confirmed.RecordsImported,
            null, account.UserId);
    }

    /// <summary>
    /// Distingue una credencial rechazada de un fallo cualquiera. Se reconoce por el
    /// contrato del adaptador y no por el texto del mensaje, que cambia sin aviso.
    /// </summary>
    private static bool IsInvalidCredential(Exception exception) =>
        exception is IInvalidCredentialSignal { IsInvalidCredential: true };
}

/// <summary>
/// Lo implementan las excepciones de los adaptadores que saben distinguir una
/// credencial rechazada de un fallo temporal de la plataforma.
/// </summary>
public interface IInvalidCredentialSignal
{
    bool IsInvalidCredential { get; }
}
