using Kapea.Domain.Common;
using Kapea.Domain.Lots;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Lots;

public class LotTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());

    [Fact]
    public void A_new_lot_starts_with_its_whole_quantity_remaining()
    {
        var lot = CreateLot(quantity: 10m, cost: 1000m);

        Assert.Equal(new Quantity(10m), lot.OriginalQuantity);
        Assert.Equal(new Quantity(10m), lot.RemainingQuantity);
        Assert.Equal(Money.Euros(100m), lot.UnitCost);
    }

    [Fact]
    public void Consuming_part_of_a_lot_returns_the_proportional_cost()
    {
        var lot = CreateLot(quantity: 10m, cost: 1000m);

        var cost = lot.Consume(new Quantity(3m));

        Assert.Equal(Money.Euros(300m), cost);
        Assert.Equal(new Quantity(7m), lot.RemainingQuantity);
    }

    [Fact]
    public void Consuming_more_than_remaining_throws_instead_of_going_negative()
    {
        var lot = CreateLot(quantity: 10m, cost: 1000m);
        lot.Consume(new Quantity(9m));

        var exception = Assert.Throws<LotOverconsumptionException>(() => lot.Consume(new Quantity(1.5m)));

        Assert.Equal(new Quantity(1m), exception.Remaining);
        Assert.Equal(new Quantity(1m), lot.RemainingQuantity);
    }

    [Fact]
    public void A_fully_consumed_lot_is_exhausted()
    {
        var lot = CreateLot(quantity: 10m, cost: 1000m);

        lot.Consume(new Quantity(10m));

        Assert.True(lot.IsExhausted);
    }

    [Fact]
    public void A_lot_cost_must_be_in_euros() =>
        Assert.Throws<DomainException>(() => Lot.Create(
            Owner, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Quantity(1m),
            new Money(100m, Currency.FromCode("USD")), Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"), 1));

    [Fact]
    public void A_lot_without_quantity_is_rejected() =>
        Assert.Throws<DomainException>(() => CreateLot(quantity: 0m, cost: 100m));

    [Fact]
    public void A_split_multiplies_the_quantity_and_keeps_the_total_cost()
    {
        var lot = CreateLot(quantity: 10m, cost: 1000m);

        lot.ApplySplit(4m);

        Assert.Equal(new Quantity(40m), lot.RemainingQuantity);
        Assert.Equal(Money.Euros(1000m), lot.AcquisitionCost);
        Assert.Equal(Money.Euros(25m), lot.UnitCost);
    }

    [Fact]
    public void A_non_positive_split_ratio_is_rejected() =>
        Assert.Throws<DomainException>(() => CreateLot(10m, 1000m).ApplySplit(0m));

    [Fact]
    public void Transferring_a_lot_keeps_its_cost_and_acquisition_date()
    {
        var acquiredAt = Occurrence.FromOffset(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), "UTC");
        var lot = CreateLot(quantity: 10m, cost: 1000m, acquiredAt: acquiredAt);
        var destination = Guid.NewGuid();

        lot.TransferTo(destination);

        Assert.Equal(destination, lot.AccountId);
        Assert.Equal(Money.Euros(1000m), lot.AcquisitionCost);
        Assert.Equal(acquiredAt, lot.AcquiredAt);
    }

    [Fact]
    public void A_transfer_fee_increases_the_lot_cost()
    {
        var lot = CreateLot(quantity: 10m, cost: 1000m);

        lot.AddTransferFee(Money.Euros(5m));

        Assert.Equal(Money.Euros(1005m), lot.AcquisitionCost);
    }

    private static Lot CreateLot(decimal quantity, decimal cost, Occurrence? acquiredAt = null) =>
        Lot.Create(
            Owner,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Quantity(quantity),
            Money.Euros(cost),
            acquiredAt ?? Occurrence.FromOffset(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), "UTC"),
            sequenceNumber: 1);
}
