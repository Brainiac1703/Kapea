using Kapea.Application.Portfolio;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

public sealed class PortfolioCalculationRepository(KapeaDbContext context) : IPortfolioCalculationRepository
{
    public async Task<IReadOnlyList<Transaction>> ListTransactionsAsync(
        Guid? assetId,
        CancellationToken cancellationToken = default) =>
        await context.Transactions
            .Where(transaction => assetId == null || transaction.AssetId == assetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<InternalTransfer>> ListTransfersAsync(
        CancellationToken cancellationToken = default) =>
        await context.InternalTransfers.ToListAsync(cancellationToken).ConfigureAwait(false);
}
