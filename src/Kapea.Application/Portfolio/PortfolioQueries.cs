using Kapea.Shared.Contracts;

namespace Kapea.Application.Portfolio;

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
    Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrokerCredentialResponse>> ListCredentialsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportRunResponse>> ListImportRunsAsync(
        Guid? accountId,
        CancellationToken cancellationToken = default);

    Task<ImportRunResponse?> FindImportRunAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionResponse>> ListTransactionsAsync(
        Guid? accountId,
        bool onlyRequiringReview,
        CancellationToken cancellationToken = default);

    Task<PortfolioResponse> GetPortfolioAsync(CancellationToken cancellationToken = default);

    Task<TaxYearResultsResponse> GetTaxYearResultsAsync(int taxYear, CancellationToken cancellationToken = default);

    Task<RealizedResultResponse?> FindRealizedResultAsync(
        Guid disposalTransactionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InternalTransferResponse>> ListTransfersAsync(
        bool onlyPending,
        CancellationToken cancellationToken = default);
}
