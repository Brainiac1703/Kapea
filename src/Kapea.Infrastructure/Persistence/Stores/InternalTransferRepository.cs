using Kapea.Application.Portfolio;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

public sealed class InternalTransferRepository(KapeaDbContext context) : IInternalTransferRepository
{
    public async Task<IReadOnlyList<Transaction>> ListTransactionsAsync(
        CancellationToken cancellationToken = default) =>
        await context.Transactions.ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<InternalTransfer>> ListTransfersAsync(
        CancellationToken cancellationToken = default) =>
        await context.InternalTransfers.ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(
        IReadOnlyList<InternalTransfer> transfers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfers);

        await context.InternalTransfers.AddRangeAsync(transfers, cancellationToken).ConfigureAwait(false);
    }

    public async Task<InternalTransfer?> FindAsync(Guid transferId, CancellationToken cancellationToken = default) =>
        await context.InternalTransfers
            .SingleOrDefaultAsync(transfer => transfer.Id == transferId, cancellationToken)
            .ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
