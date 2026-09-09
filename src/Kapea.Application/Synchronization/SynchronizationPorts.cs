using Kapea.Domain.Accounts;
using Kapea.Domain.Credentials;

namespace Kapea.Application.Synchronization;

/// <summary>Cuenta con credencial utilizable: lo que la sincronización programada recorre.</summary>
public sealed record SynchronizationTarget(PlatformAccount Account, BrokerCredential Credential);

public interface ISynchronizationRepository
{
    Task<IReadOnlyList<SynchronizationTarget>> ListTargetsAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Exclusión mutua por cuenta.
/// </summary>
/// <remarks>
/// Dos sincronizaciones simultáneas de la misma cuenta pedirían el mismo periodo a la
/// plataforma y competirían por escribir los mismos movimientos. El cerrojo vive fuera
/// del proceso porque la sincronización se ejecuta en un proceso programado que puede
/// solaparse consigo mismo si una ejecución se alarga.
/// </remarks>
public interface IAccountSyncLock
{
    /// <summary>Devuelve null si la cuenta ya tiene una sincronización en curso.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(
        Guid accountId,
        TimeSpan lease,
        CancellationToken cancellationToken = default);
}
