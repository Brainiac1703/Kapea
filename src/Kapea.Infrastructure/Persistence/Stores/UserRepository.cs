using Kapea.Application.Identity;
using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// Acceso al registro de usuarios.
/// </summary>
/// <remarks>
/// Las consultas ignoran el filtro global a propósito: aquí todavía no se sabe quién
/// es el usuario —precisamente es lo que se está averiguando—, y el filtro dejaría el
/// registro vacío en el momento del acceso.
/// </remarks>
public sealed class UserRepository(KapeaDbContext context) : IUserRepository
{
    public async Task<User?> FindByIdentityAsync(
        string provider,
        string subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return await context.Users
            .IgnoreQueryFilters()
            .Include(user => user.Identities)
            .SingleOrDefaultAsync(
                user => user.Identities.Any(identity =>
                    identity.Provider == provider && identity.Subject == subject),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<User?> FindAsync(UserId userId, CancellationToken cancellationToken = default) =>
        await context.Users
            .IgnoreQueryFilters()
            .Include(user => user.Identities)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        await context.Users.IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await context.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Traslada la propiedad de todo lo que estuviera a nombre de un identificador.
    /// Solo se usa para adoptar los datos del usuario fijo de desarrollo.
    /// </summary>
    public async Task<int> ReassignOwnershipAsync(
        UserId from,
        UserId to,
        CancellationToken cancellationToken = default)
    {
        var movidas = 0;

        movidas += await context.Accounts.IgnoreQueryFilters()
            .Where(account => account.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(account => account.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.Transactions.IgnoreQueryFilters()
            .Where(transaction => transaction.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(transaction => transaction.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.Lots.IgnoreQueryFilters()
            .Where(lot => lot.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(lot => lot.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.RealizedResults.IgnoreQueryFilters()
            .Where(result => result.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(result => result.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.CapitalIncomes.IgnoreQueryFilters()
            .Where(income => income.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(income => income.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.BrokerCredentials.IgnoreQueryFilters()
            .Where(credential => credential.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(credential => credential.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.ImportRuns.IgnoreQueryFilters()
            .Where(run => run.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(run => run.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        movidas += await context.InternalTransfers.IgnoreQueryFilters()
            .Where(transfer => transfer.UserId == from)
            .ExecuteUpdateAsync(set => set.SetProperty(transfer => transfer.UserId, to), cancellationToken)
            .ConfigureAwait(false);

        return movidas;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
