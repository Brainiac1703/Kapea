using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Transfers;

public class InternalTransferDetectorTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Asset = Guid.NewGuid();
    private static readonly Guid OtherAsset = Guid.NewGuid();
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Destination = Guid.NewGuid();
    private static readonly DateTimeOffset Sent = new(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_withdrawal_matched_by_a_deposit_within_the_window_is_proposed()
    {
        var outgoing = Withdrawal(1m, Sent);
        var incoming = Deposit(1m, Sent.AddHours(2));

        var candidate = Assert.Single(Propose(outgoing, incoming));

        Assert.Equal(outgoing.Id, candidate.Outgoing.Id);
        Assert.Equal(incoming.Id, candidate.Incoming.Id);
    }

    [Fact]
    public void A_deposit_outside_the_window_is_not_proposed() =>
        Assert.Empty(Propose(Withdrawal(1m, Sent), Deposit(1m, Sent.AddHours(73))));

    [Fact]
    public void A_deposit_before_the_withdrawal_is_not_proposed() =>
        // El activo no llega antes de salir: emparejarlo relacionaría operaciones ajenas.
        Assert.Empty(Propose(Withdrawal(1m, Sent), Deposit(1m, Sent.AddHours(-1))));

    [Fact]
    public void A_deposit_reduced_by_a_network_fee_is_still_proposed()
    {
        // Un 1 % de merma cabe en la tolerancia del 2 %.
        var candidate = Assert.Single(Propose(Withdrawal(1m, Sent), Deposit(0.99m, Sent.AddHours(1))));

        Assert.Equal(new Quantity(0.99m), candidate.Incoming.Quantity);
    }

    [Fact]
    public void A_deposit_too_far_from_the_amount_sent_is_not_proposed() =>
        Assert.Empty(Propose(Withdrawal(1m, Sent), Deposit(0.9m, Sent.AddHours(1))));

    [Fact]
    public void Receiving_more_than_was_sent_is_not_a_transfer() =>
        Assert.Empty(Propose(Withdrawal(1m, Sent), Deposit(1.5m, Sent.AddHours(1))));

    [Fact]
    public void A_deposit_of_another_asset_is_not_proposed() =>
        Assert.Empty(Propose(Withdrawal(1m, Sent), Deposit(1m, Sent.AddHours(1), assetId: OtherAsset)));

    [Fact]
    public void A_deposit_in_the_same_account_is_not_a_transfer() =>
        Assert.Empty(Propose(Withdrawal(1m, Sent), Deposit(1m, Sent.AddHours(1), accountId: Source)));

    [Fact]
    public void A_pairing_the_user_already_rejected_is_not_proposed_again()
    {
        var outgoing = Withdrawal(1m, Sent);
        var incoming = Deposit(1m, Sent.AddHours(1));

        var candidates = InternalTransferDetector.Propose(
            [outgoing, incoming],
            new HashSet<(Guid, Guid)> { (outgoing.Id, incoming.Id) });

        Assert.Empty(candidates);
    }

    [Fact]
    public void Each_deposit_is_matched_at_most_once()
    {
        var first = Withdrawal(1m, Sent);
        var second = Withdrawal(1m, Sent.AddMinutes(5));
        var incoming = Deposit(1m, Sent.AddHours(1));

        var candidate = Assert.Single(InternalTransferDetector.Propose(
            [first, second, incoming], new HashSet<(Guid, Guid)>()));

        Assert.Equal(first.Id, candidate.Outgoing.Id);
    }

    [Fact]
    public void The_window_and_the_tolerance_are_configurable()
    {
        var options = new TransferMatchingOptions(TimeSpan.FromHours(1), 0.5m);

        Assert.Single(InternalTransferDetector.Propose(
            [Withdrawal(1m, Sent), Deposit(0.6m, Sent.AddMinutes(30))], new HashSet<(Guid, Guid)>(), options));
    }

    [Fact]
    public void Confirming_records_the_network_fee_as_the_difference()
    {
        var transfer = InternalTransfer.Propose(
            Owner, Guid.NewGuid(), Guid.NewGuid(), Source, Destination, Asset,
            new Quantity(1m), new Quantity(0.99m), Sent);

        transfer.Confirm(Sent.AddDays(1));

        Assert.True(transfer.IsConfirmed);
        Assert.False(transfer.IsPending);
        Assert.Equal(new Quantity(0.01m), transfer.NetworkFeeQuantity);
    }

    [Fact]
    public void A_rejected_pairing_is_no_longer_pending()
    {
        var transfer = Proposed();

        transfer.Reject(Sent.AddDays(1));

        Assert.Equal(InternalTransferStatus.Rejected, transfer.Status);
        Assert.False(transfer.IsPending);
    }

    [Fact]
    public void A_resolved_pairing_cannot_be_resolved_again()
    {
        var transfer = Proposed();
        transfer.Confirm(Sent);

        Assert.Throws<DomainException>(() => transfer.Reject(Sent));
    }

    [Fact]
    public void A_transfer_within_a_single_account_is_rejected() =>
        Assert.Throws<DomainException>(() => InternalTransfer.Propose(
            Owner, Guid.NewGuid(), Guid.NewGuid(), Source, Source, Asset,
            new Quantity(1m), new Quantity(1m), Sent));

    private static InternalTransfer Proposed() => InternalTransfer.Propose(
        Owner, Guid.NewGuid(), Guid.NewGuid(), Source, Destination, Asset,
        new Quantity(1m), new Quantity(1m), Sent);

    private static IReadOnlyList<TransferCandidate> Propose(Transaction outgoing, Transaction incoming) =>
        InternalTransferDetector.Propose([outgoing, incoming], new HashSet<(Guid, Guid)>());

    private static Transaction Withdrawal(decimal quantity, DateTimeOffset at, Guid? accountId = null) =>
        Build(TransactionType.Withdrawal, quantity, at, accountId ?? Source, Asset);

    private static Transaction Deposit(decimal quantity, DateTimeOffset at, Guid? accountId = null, Guid? assetId = null) =>
        Build(TransactionType.Deposit, quantity, at, accountId ?? Destination, assetId ?? Asset);

    private static Transaction Build(
        TransactionType type, decimal quantity, DateTimeOffset at, Guid accountId, Guid assetId) =>
        Transaction.Imported(
            Owner, accountId, type, assetId, new Quantity(quantity), null, Money.Euros(0m), Money.Euros(0m),
            Occurrence.FromOffset(at, "UTC"),
            TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString()));
}
