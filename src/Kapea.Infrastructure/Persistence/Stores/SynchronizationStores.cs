using Kapea.Application.Synchronization;
using Kapea.Domain.Credentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Persistence.Stores;

public sealed class SynchronizationRepository(KapeaDbContext context) : ISynchronizationRepository
{
    public async Task<IReadOnlyList<SynchronizationTarget>> ListTargetsAsync(
        CancellationToken cancellationToken = default)
    {
        // Solo las cuentas con credencial activa: una revocada o marcada como inválida
        // queda fuera hasta que el usuario la rote.
        // IgnoreQueryFilters a propósito: este barrido recorre las cuentas de todos los
        // usuarios, y a partir de aquí cada una se procesa declarando su propio dueño.
        var credentials = await context.BrokerCredentials
            .IgnoreQueryFilters()
            .Where(credential => credential.Status == CredentialStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var accountIds = credentials.Select(credential => credential.AccountId).ToList();

        var accounts = await context.Accounts
            .IgnoreQueryFilters()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. credentials
                .Where(credential => accounts.ContainsKey(credential.AccountId))
                .Select(credential => new SynchronizationTarget(accounts[credential.AccountId], credential)),
        ];
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Cerrojo por cuenta apoyado en una fila de la base de datos.
/// </summary>
/// <remarks>
/// Vive en la base de datos y no en memoria porque el proceso programado puede
/// solaparse consigo mismo o ejecutarse en más de una instancia. La fila lleva
/// caducidad para que un proceso que muera sin soltar el cerrojo no bloquee la cuenta
/// para siempre.
/// </remarks>
public sealed class AccountSyncLock(KapeaDbContext context, TimeProvider timeProvider, ILogger<AccountSyncLock> logger)
    : IAccountSyncLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        Guid accountId,
        TimeSpan lease,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var token = Guid.NewGuid();

        // Una sola sentencia decide: inserta si no hay fila, y la toma si la que hay ha
        // caducado. Hacerlo en dos pasos abriría una carrera entre comprobar y escribir.
        var affected = await context.Database.ExecuteSqlAsync(
            $"""
            MERGE dbo.AccountSyncLocks WITH (HOLDLOCK) AS target
            USING (SELECT {accountId} AS AccountId) AS source ON target.AccountId = source.AccountId
            WHEN MATCHED AND target.ExpiresAt <= {now} THEN
                UPDATE SET Token = {token}, AcquiredAt = {now}, ExpiresAt = {now.Add(lease)}
            WHEN NOT MATCHED THEN
                INSERT (AccountId, Token, AcquiredAt, ExpiresAt)
                VALUES ({accountId}, {token}, {now}, {now.Add(lease)});
            """,
            cancellationToken).ConfigureAwait(false);

        if (affected == 0)
        {
            logger.LogWarning("La cuenta {Cuenta} ya tiene una sincronización en curso.", accountId);

            return null;
        }

        return new Lease(context, accountId, token);
    }

    private sealed class Lease(KapeaDbContext context, Guid accountId, Guid token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() =>
            // Se comprueba el token al soltar: si el cerrojo caducó y otro proceso lo
            // tomó, este no puede quitárselo.
            await context.Database.ExecuteSqlAsync(
                $"DELETE FROM dbo.AccountSyncLocks WHERE AccountId = {accountId} AND Token = {token}")
                .ConfigureAwait(false);
    }
}
