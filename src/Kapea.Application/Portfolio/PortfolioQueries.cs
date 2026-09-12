using Kapea.Shared.Contracts;

namespace Kapea.Application.Portfolio;

/// <summary>Qué movimientos se piden.</summary>
/// <param name="Search">Busca en el activo y en el contenido original de la fila.</param>
public sealed record TransactionQuery(
    Guid? AccountId = null,
    string? AssetSymbol = null,
    string? Type = null,
    int? Year = null,
    bool OnlyRequiringReview = false,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>
/// Consultas de lectura que devuelven directamente los contratos del cliente.
/// </summary>
/// <remarks>
/// Devuelven DTOs y no entidades porque una lectura no necesita el modelo de dominio
/// completo, y porque así el contrato con el cliente es explícito y no un reflejo
/// accidental de la forma interna de las entidades.
/// </remarks>
public interface IPortfolioQueries
{
    Task<IReadOnlyList<PlatformResponse>> ListPlatformsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrokerCredentialResponse>> ListCredentialsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportRunResponse>> ListImportRunsAsync(
        Guid? accountId,
        CancellationToken cancellationToken = default);

    Task<ImportRunResponse?> FindImportRunAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>Una página de movimientos, con los filtros ya aplicados.</summary>
    Task<TransactionPageResponse> SearchTransactionsAsync(
        TransactionQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionResponse>> ListTransactionsAsync(
        Guid? accountId,
        bool onlyRequiringReview,
        CancellationToken cancellationToken = default);

    Task<PortfolioResponse> GetPortfolioAsync(CancellationToken cancellationToken = default);

    /// <summary>Evolución de la cartera entre dos fechas, con el reparto por clase.</summary>
    Task<PortfolioHistoryResponse> GetHistoryAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rendimiento del periodo, con la referencia contra la que compararlo.
    /// </summary>
    /// <param name="benchmarkAssetId">
    /// Activo con el que comparar. Sin él se toma la mayor posición, que es la
    /// alternativa más cercana a no haber hecho nada con ese dinero.
    /// </param>
    Task<PerformanceResponse> GetPerformanceAsync(
        DateOnly from,
        DateOnly to,
        Guid? benchmarkAssetId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Evolución de un activo con sus indicadores, o nada si no está en cartera.</summary>
    Task<AssetHistoryResponse?> GetAssetHistoryAsync(
        Guid assetId,
        DateOnly from,
        DateOnly to,
        int indicatorWindowDays,
        CancellationToken cancellationToken = default);

    Task<TaxYearResultsResponse> GetTaxYearResultsAsync(int taxYear, CancellationToken cancellationToken = default);

    Task<RealizedResultResponse?> FindRealizedResultAsync(
        Guid disposalTransactionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InternalTransferResponse>> ListTransfersAsync(
        bool onlyPending,
        CancellationToken cancellationToken = default);
}
