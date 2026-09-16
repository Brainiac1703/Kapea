using Kapea.Application.Portfolio;
using Kapea.Domain.Accounts;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

internal sealed class ManualMovementRepository(KapeaDbContext context) : IManualMovementRepository
{
    public Task<PlatformAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        context.Accounts.SingleOrDefaultAsync(account => account.Id == accountId, cancellationToken);

    /// <summary>Con los anulados: restaurar uno exige encontrarlo. El filtro de usuario sigue puesto.</summary>
    public Task<Transaction?> FindTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        context.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .SingleOrDefaultAsync(transaction => transaction.Id == transactionId, cancellationToken);

    public async Task<Guid?> FindConfirmedTransferAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
        await context.InternalTransfers
            .Where(transfer => transfer.Status == InternalTransferStatus.Confirmed
                && (transfer.OutgoingTransactionId == transactionId || transfer.IncomingTransactionId == transactionId))
            .Select(transfer => (Guid?)transfer.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Transaction transaction) => context.Transactions.Add(transaction);

    public void Remove(Transaction transaction) => context.Transactions.Remove(transaction);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}
