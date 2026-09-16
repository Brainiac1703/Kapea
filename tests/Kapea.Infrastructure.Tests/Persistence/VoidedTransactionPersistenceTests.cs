using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class VoidedTransactionPersistenceTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_voided_movement_is_left_out_of_ordinary_queries()
    {
        var (owner, account, voided, inForce) = await SeedAsync();

        await using var context = fixture.CreateContext(owner);
        var ids = await context.Transactions.Where(t => t.AccountId == account).Select(t => t.Id).ToListAsync();

        Assert.Contains(inForce, ids);
        Assert.DoesNotContain(voided, ids);
    }

    [Fact]
    public async Task Lifting_only_the_in_force_filter_shows_voided_movements()
    {
        var (owner, account, voided, _) = await SeedAsync();

        await using var context = fixture.CreateContext(owner);
        var stored = await context.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .SingleAsync(t => t.Id == voided);

        Assert.True(stored.IsVoided);
        Assert.Equal("duplicada", stored.VoidReason);
    }

    [Fact]
    public async Task Lifting_the_in_force_filter_never_shows_someone_elses_movements()
    {
        var (_, _, voided, inForce) = await SeedAsync();

        await using var stranger = fixture.CreateContext(new UserId(Guid.NewGuid()));
        var ids = await stranger.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .Select(t => t.Id)
            .ToListAsync();

        Assert.DoesNotContain(voided, ids);
        Assert.DoesNotContain(inForce, ids);
    }

    [Fact]
    public async Task The_fingerprint_of_a_voided_movement_still_counts_as_existing()
    {
        var owner = new UserId(Guid.NewGuid());
        var account = Guid.NewGuid();
        var imported = Imported(owner, account);

        await using (var writer = fixture.CreateContext(owner))
        {
            imported.Void("duplicada", Now);
            writer.Transactions.Add(imported);
            await writer.SaveChangesAsync();
        }

        await using var context = fixture.CreateContext(owner);
        var existing = await new ImportRepository(context)
            .FindExistingFingerprintsAsync(account, [imported.Source.Fingerprint]);

        Assert.Contains(imported.Source.Fingerprint, existing);
    }

    [Fact]
    public async Task A_manual_movement_keeps_its_note_and_its_moments()
    {
        var owner = new UserId(Guid.NewGuid());
        var manual = Transaction.FromManualEntry(
            owner, Guid.NewGuid(), TransactionType.Buy, Guid.NewGuid(), new Quantity(2m), Money.Euros(10m),
            Money.Euros(20m), Money.Euros(0m), Occurrence.FromOffset(Now, "Europe/Madrid"), Now, "compra en papel");

        await using (var writer = fixture.CreateContext(owner))
        {
            writer.Transactions.Add(manual);
            await writer.SaveChangesAsync();
        }

        await using var context = fixture.CreateContext(owner);
        var stored = await context.Transactions.SingleAsync(t => t.Id == manual.Id);

        Assert.Equal(TransactionOrigin.Manual, stored.Origin);
        Assert.Equal("compra en papel", stored.Note);
        Assert.Equal(Now, stored.RegisteredAt);
    }

    private async Task<(UserId Owner, Guid Account, Guid Voided, Guid InForce)> SeedAsync()
    {
        var owner = new UserId(Guid.NewGuid());
        var account = Guid.NewGuid();
        var voided = Imported(owner, account);
        var inForce = Imported(owner, account);

        voided.Void("duplicada", Now);

        await using var writer = fixture.CreateContext(owner);
        writer.Transactions.AddRange(voided, inForce);
        await writer.SaveChangesAsync();

        return (owner, account, voided.Id, inForce.Id);
    }

    private static Transaction Imported(UserId owner, Guid account) =>
        Transaction.Imported(
            owner, account, TransactionType.Buy, Guid.NewGuid(), new Quantity(1m), Money.Euros(10m), Money.Euros(10m),
            Money.Euros(0m), Occurrence.FromOffset(Now, "UTC"),
            TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString("N"), null, Guid.NewGuid().ToString("N")));
}
