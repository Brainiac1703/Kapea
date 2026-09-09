using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Import;
using Kapea.Domain.Transactions;

namespace Kapea.Application.Import;

/// <summary>
/// Resuelve el símbolo que trae un origen contra el catálogo de activos. Cuando no se
/// puede resolver, crea el activo como no verificado en lugar de bloquear la
/// importación entera por un símbolo desconocido.
/// </summary>
public interface IAssetCatalog
{
    Task<Asset> ResolveAsync(string symbol, AssetClass assetClass, CancellationToken cancellationToken = default);
}

/// <summary>
/// Acceso a lo que el motor de importación necesita persistir. Es un puerto acotado y
/// no el contexto entero para que la capa de aplicación no dependa de EF Core.
/// </summary>
public interface IImportRepository
{
    Task<PlatformAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<ImportRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportRun>> ListRunsAsync(Guid? accountId, CancellationToken cancellationToken = default);

    Task AddRunAsync(ImportRun run, CancellationToken cancellationToken = default);

    Task RemoveRunAsync(ImportRun run, CancellationToken cancellationToken = default);

    /// <summary>Huellas ya presentes en la cuenta, de entre las que se le pasan.</summary>
    Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
        Guid accountId,
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default);

    Task AddTransactionsAsync(IReadOnlyList<Transaction> transactions, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> ListTransactionsOfRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task RemoveTransactionsAsync(IReadOnlyList<Transaction> transactions, CancellationToken cancellationToken = default);

    /// <summary>Instante hasta el que llegó la última importación correcta de la cuenta.</summary>
    Task<DateTimeOffset?> FindLastSuccessfulImportInstantAsync(
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta el trabajo en una transacción. La confirmación de una importación es
    /// todo o nada: un fallo a mitad no puede dejar movimientos sueltos.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);
}
