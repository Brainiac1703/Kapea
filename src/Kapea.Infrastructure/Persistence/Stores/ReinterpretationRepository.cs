using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>Los movimientos sin clasificar del usuario, con la plataforma de su cuenta.</summary>
public sealed class ReinterpretationRepository(KapeaDbContext context) : IReinterpretationRepository
{
    public async Task<IReadOnlyList<(Transaction Transaction, PlatformCode Platform)>> ListUnclassifiedAsync(
        CancellationToken cancellationToken = default)
    {
        // La plataforma viene de la cuenta y no del movimiento: es la que decide con qué
        // reglas se leyó, y el movimiento no la guarda.
        var pending = await context.Transactions
            .Where(transaction => transaction.Type == TransactionType.Unknown)
            .Join(
                context.Accounts,
                transaction => transaction.AccountId,
                account => account.Id,
                (transaction, account) => new { transaction, account.Platform })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. pending.Select(entry => (entry.transaction, entry.Platform))];
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
