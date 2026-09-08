using Kapea.Application.Portfolio;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.Tests.Portfolio;

public class PortfolioCalculationServiceTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Asset = Guid.NewGuid();
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();
    private static readonly DateTimeOffset Moment = new(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_pending_transfer_marks_both_legs_as_unresolved()
    {
        var (outgoing, incoming, transfer) = Pair();

        var valued = PortfolioCalculationService.Value([outgoing, incoming], [transfer]);

        Assert.All(valued, item => Assert.True(item.IsUnresolved));
    }

    [Fact]
    public void A_confirmed_transfer_turns_the_outgoing_leg_into_a_lot_move()
    {
        var (outgoing, incoming, transfer) = Pair();
        transfer.Confirm(Moment);

        var valued = PortfolioCalculationService.Value([outgoing, incoming], [transfer]);

        var sent = valued.Single(item => item.Transaction.Id == outgoing.Id);
        var received = valued.Single(item => item.Transaction.Id == incoming.Id);

        Assert.True(sent.IsInternalTransferOut);
        Assert.Equal(Destination, sent.TransferDestinationAccountId);
        Assert.True(received.IsInternalTransferIn);
        Assert.False(sent.IsUnresolved);
    }

    [Fact]
    public void A_rejected_transfer_leaves_both_movements_as_independent_operations()
    {
        var (outgoing, incoming, transfer) = Pair();
        transfer.Reject(Moment);

        var valued = PortfolioCalculationService.Value([outgoing, incoming], [transfer]);

        Assert.All(valued, item =>
        {
            Assert.False(item.IsUnresolved);
            Assert.False(item.IsInternalTransferOut);
            Assert.False(item.IsInternalTransferIn);
        });
    }

    [Fact]
    public void A_movement_with_no_transfer_at_all_is_valued_as_itself()
    {
        var transaction = Movement(TransactionType.Buy, Source);

        var valued = Assert.Single(PortfolioCalculationService.Value([transaction], []));

        Assert.False(valued.IsUnresolved);
        Assert.Null(valued.InternalTransferId);
    }

    private static (Transaction Outgoing, Transaction Incoming, InternalTransfer Transfer) Pair()
    {
        var outgoing = Movement(TransactionType.Withdrawal, Source);
        var incoming = Movement(TransactionType.Deposit, Destination);

        var transfer = InternalTransfer.Propose(
            Owner, outgoing.Id, incoming.Id, Source, Destination, Asset,
            new Quantity(1m), new Quantity(1m), Moment);

        return (outgoing, incoming, transfer);
    }

    private static Transaction Movement(TransactionType type, Guid accountId) =>
        Transaction.Imported(
            Owner, accountId, type, Asset, new Quantity(1m), null, Money.Euros(0m), Money.Euros(0m),
            Occurrence.FromOffset(Moment, "UTC"),
            TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString()));
}
