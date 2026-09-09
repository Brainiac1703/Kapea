using Kapea.Domain.Transfers;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class InternalTransferPersistenceTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_rejected_pairing_stays_rejected_and_is_not_proposed_again()
    {
        var owner = new UserId(Guid.NewGuid());
        var transfer = Propose(owner);

        await using (var context = fixture.CreateContext(owner))
        {
            transfer.Reject(Now);
            context.InternalTransfers.Add(transfer);
            await context.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(owner);
        var stored = await reader.InternalTransfers.SingleAsync(entity => entity.Id == transfer.Id);

        Assert.Equal(InternalTransferStatus.Rejected, stored.Status);
        Assert.False(stored.IsPending);

        // Es exactamente lo que el detector consulta para no volver a proponerlo.
        var resolved = await reader.InternalTransfers
            .Where(entity => entity.Status != InternalTransferStatus.Proposed)
            .Select(entity => new { entity.OutgoingTransactionId, entity.IncomingTransactionId })
            .ToListAsync();

        Assert.Contains(
            resolved,
            pair => pair.OutgoingTransactionId == transfer.OutgoingTransactionId
                && pair.IncomingTransactionId == transfer.IncomingTransactionId);
    }

    [Fact]
    public async Task A_confirmed_transfer_keeps_its_network_fee()
    {
        var owner = new UserId(Guid.NewGuid());
        var transfer = Propose(owner, sent: 1m, received: 0.99m);

        await using (var context = fixture.CreateContext(owner))
        {
            transfer.Confirm(Now);
            context.InternalTransfers.Add(transfer);
            await context.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(owner);
        var stored = await reader.InternalTransfers.SingleAsync(entity => entity.Id == transfer.Id);

        Assert.True(stored.IsConfirmed);
        Assert.Equal(new Quantity(0.01m), stored.NetworkFeeQuantity);
    }

    [Fact]
    public async Task The_same_pairing_cannot_be_stored_twice()
    {
        var owner = new UserId(Guid.NewGuid());
        var first = Propose(owner);

        await using (var context = fixture.CreateContext(owner))
        {
            context.InternalTransfers.Add(first);
            await context.SaveChangesAsync();
        }

        var duplicate = InternalTransfer.Propose(
            owner, first.OutgoingTransactionId, first.IncomingTransactionId, first.SourceAccountId,
            first.DestinationAccountId, first.AssetId, first.SentQuantity, first.ReceivedQuantity, Now);

        await using var writer = fixture.CreateContext(owner);
        writer.InternalTransfers.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => writer.SaveChangesAsync());
    }

    [Fact]
    public async Task A_transfer_of_another_user_is_not_visible()
    {
        var owner = new UserId(Guid.NewGuid());
        var transfer = Propose(owner);

        await using (var context = fixture.CreateContext(owner))
        {
            context.InternalTransfers.Add(transfer);
            await context.SaveChangesAsync();
        }

        await using var stranger = fixture.CreateContext(new UserId(Guid.NewGuid()));

        Assert.Empty(await stranger.InternalTransfers.Where(entity => entity.Id == transfer.Id).ToListAsync());
    }

    private static InternalTransfer Propose(UserId owner, decimal sent = 1m, decimal received = 1m) =>
        InternalTransfer.Propose(
            owner, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new Quantity(sent), new Quantity(received), Now);
}
