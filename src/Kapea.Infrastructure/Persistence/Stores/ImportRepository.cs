using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Import;
using Kapea.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>Acceso a datos del motor de importación sobre EF Core.</summary>
public sealed class ImportRepository(KapeaDbContext context) : IImportRepository
{
    public async Task<PlatformAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        await context.Accounts.SingleOrDefaultAsync(account => account.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ImportRun?> FindRunAsync(Guid runId, CancellationToken cancellationToken = default) =>
        await context.ImportRuns
            .Include(run => run.Records)
            .SingleOrDefaultAsync(run => run.Id == runId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ImportRun>> ListRunsAsync(
        Guid? accountId,
        CancellationToken cancellationToken = default) =>
        await context.ImportRuns
            .Include(run => run.Records)
            .Where(run => accountId == null || run.AccountId == accountId)
            .OrderByDescending(run => run.StartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddRunAsync(ImportRun run, CancellationToken cancellationToken = default)
    {
        await context.ImportRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveRunAsync(ImportRun run, CancellationToken cancellationToken = default)
    {
        context.ImportRuns.Remove(run);

        return Task.CompletedTask;
    }

    public async Task<IReadOnlySet<string>> FindExistingFingerprintsAsync(
        Guid accountId,
        IReadOnlyCollection<string> fingerprints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);

        if (fingerprints.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var candidates = fingerprints.ToList();

        var found = await context.Transactions
            .Where(transaction => transaction.AccountId == accountId
                && candidates.Contains(transaction.Source.Fingerprint))
            .Select(transaction => transaction.Source.Fingerprint)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return found.ToHashSet(StringComparer.Ordinal);
    }

    public async Task AddTransactionsAsync(
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        await context.Transactions.AddRangeAsync(transactions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Transaction>> ListTransactionsOfRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default) =>
        await context.Transactions
            .Where(transaction => transaction.Source.ImportRunId == runId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task RemoveTransactionsAsync(
        IReadOnlyList<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        context.Transactions.RemoveRange(transactions);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Solo cuentan las ejecuciones completadas: una fallida no puede hacer avanzar el
    /// punto desde el que sincroniza la siguiente, o se perdería lo que no llegó a entrar.
    /// </summary>
    public async Task<DateTimeOffset?> FindLastSuccessfulImportInstantAsync(
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        await context.ImportRuns
            .Where(run => run.AccountId == accountId && run.Status == ImportRunStatus.Completed)
            .MaxAsync(run => (DateTimeOffset?)run.CoversUntil, cancellationToken)
            .ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await context.Database.ExecuteAsync(work, cancellationToken).ConfigureAwait(false);
    }
}
