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

public sealed record AccountSynchronizationResult(
    Guid AccountId,
    Platform Platform,
    SynchronizationOutcome Outcome,
    int ImportedRecords,
    string? Detail);

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
    IAccountSyncLock accountLock,
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

    public async Task<SynchronizationReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var targets = await repository.ListTargetsAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<AccountSynchronizationResult>();

        logger.LogInformation("Sincronización iniciada para {Cuentas} cuentas.", targets.Count);

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(await SynchroniseAsync(target, cancellationToken).ConfigureAwait(false));
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var report = new SynchronizationReport(results);

        logger.LogInformation(
            "Sincronización terminada: {Correctas} cuentas importadas y {Fallidas} con fallo.",
            report.ImportedAccounts, report.FailedAccounts);

        return report;
    }

    private async Task<AccountSynchronizationResult> SynchroniseAsync(
        SynchronizationTarget target,
        CancellationToken cancellationToken)
    {
        var account = target.Account;

        await using var lease = await accountLock
            .TryAcquireAsync(account.Id, LockLease, cancellationToken).ConfigureAwait(false);

        if (lease is null)
        {
            logger.LogWarning(
                "Sincronización de la cuenta {Cuenta} omitida: ya hay otra en curso.", account.Id);

            return new AccountSynchronizationResult(
                account.Id, account.Platform, SynchronizationOutcome.SkippedOverlapping, 0,
                "Ya había una sincronización en curso.");
        }

        try
        {
            return await ImportAsync(target, cancellationToken).ConfigureAwait(false);
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
                    account.Id, account.Platform, SynchronizationOutcome.CredentialInvalid, 0, exception.Message);
            }

            return new AccountSynchronizationResult(
                account.Id, account.Platform, SynchronizationOutcome.Failed, 0, exception.Message);
        }
    }

    private async Task<AccountSynchronizationResult> ImportAsync(
        SynchronizationTarget target,
        CancellationToken cancellationToken)
    {
        var account = target.Account;
        var secret = await credentials.ResolveAsync(target.Credential, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No se ha podido recuperar el secreto de la cuenta {account.Id}.");

        // Se pide solo lo posterior a la última importación correcta. Una fallida no
        // mueve ese punto, así que lo que no llegó a entrar se vuelve a pedir.
        var from = await importRepository
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
            account.Id, account.Platform, SynchronizationOutcome.Imported, confirmed.RecordsImported, null);
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
