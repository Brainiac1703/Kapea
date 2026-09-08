using Kapea.Domain.Calculation;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class FifoCalculatorTests
{
    [Fact]
    public void A_sale_smaller_than_the_oldest_lot_consumes_it_partially()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Buy("2024-06-10", quantity: 10m, grossEuros: 1500m)
            .Sell("2025-02-01", quantity: 4m, grossEuros: 800m)
            .Calculate();

        var realized = Assert.Single(result.RealizedResults);
        var consumed = Assert.Single(realized.ConsumedLots);

        Assert.Equal(new Quantity(4m), consumed.Quantity);
        Assert.Equal(Money.Euros(400m), realized.AcquisitionCostInEuros);
        Assert.Equal(Money.Euros(800m), realized.ProceedsInEuros);
        Assert.Equal(Money.Euros(400m), realized.ResultInEuros);
        Assert.Equal(new Quantity(6m), result.OpenLots[0].RemainingQuantity);
        Assert.Equal(new Quantity(10m), result.OpenLots[1].RemainingQuantity);
    }

    [Fact]
    public void A_sale_spanning_several_lots_consumes_them_oldest_first_and_reports_the_breakdown()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Buy("2024-06-10", quantity: 10m, grossEuros: 1500m)
            .Sell("2025-02-01", quantity: 15m, grossEuros: 3000m)
            .Calculate();

        var realized = Assert.Single(result.RealizedResults);

        Assert.Equal(2, realized.ConsumedLots.Count);
        Assert.Equal(new Quantity(10m), realized.ConsumedLots[0].Quantity);
        Assert.Equal(Money.Euros(1000m), realized.ConsumedLots[0].AcquisitionCostInEuros);
        Assert.Equal(new Quantity(5m), realized.ConsumedLots[1].Quantity);
        Assert.Equal(Money.Euros(750m), realized.ConsumedLots[1].AcquisitionCostInEuros);
        Assert.Equal(Money.Euros(1750m), realized.AcquisitionCostInEuros);
        Assert.Equal(Money.Euros(1250m), realized.ResultInEuros);
        Assert.Equal(new Quantity(5m), Assert.Single(result.OpenLots).RemainingQuantity);
    }

    [Fact]
    public void A_sale_that_exhausts_every_lot_leaves_no_open_position()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Sell("2025-02-01", quantity: 10m, grossEuros: 1400m)
            .Calculate();

        Assert.Empty(result.OpenLots);
        Assert.Equal(Money.Euros(400m), Assert.Single(result.RealizedResults).ResultInEuros);
    }

    [Fact]
    public void Lots_held_in_different_accounts_are_consumed_by_global_acquisition_date()
    {
        // El FIFO es por activo, no por cuenta: el lote más antiguo se consume primero
        // aunque esté en otra cuenta del usuario.
        var ledger = new Ledger();

        var result = ledger
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m, accountId: ledger.OtherAccountId)
            .Buy("2024-06-10", quantity: 10m, grossEuros: 2000m, accountId: ledger.AccountId)
            .Sell("2025-02-01", quantity: 10m, grossEuros: 2500m, accountId: ledger.AccountId)
            .Calculate();

        var realized = Assert.Single(result.RealizedResults);

        Assert.Equal(Money.Euros(1000m), realized.AcquisitionCostInEuros);
        Assert.Equal(ledger.OtherAccountId, Assert.Single(realized.ConsumedLots) is { } consumed
            ? result.Lots.First(lot => lot.Id == consumed.LotId).AccountId
            : Guid.Empty);
    }

    [Fact]
    public void Recalculating_the_same_history_in_a_different_order_gives_the_same_result()
    {
        var ledger = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1200m)
            .Buy("2024-06-10", quantity: 5m, grossEuros: 900m)
            .Sell("2025-02-01", quantity: 12m, grossEuros: 2400m);

        var first = ledger.Calculate();
        var second = ledger.CalculateInReverse();

        Assert.Equal(
            first.RealizedResults.Select(r => (r.ProceedsInEuros, r.AcquisitionCostInEuros)),
            second.RealizedResults.Select(r => (r.ProceedsInEuros, r.AcquisitionCostInEuros)));
        Assert.Equal(
            first.OpenLots.Select(l => (l.RemainingQuantity, l.AcquisitionCost)),
            second.OpenLots.Select(l => (l.RemainingQuantity, l.AcquisitionCost)));
    }

    [Fact]
    public void A_sale_without_enough_lots_reports_an_inconsistency_instead_of_a_partial_result()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 3m, grossEuros: 300m)
            .Sell("2025-02-01", quantity: 5m, grossEuros: 700m)
            .Calculate();

        Assert.Empty(result.RealizedResults);

        var inconsistency = Assert.Single(result.Inconsistencies);

        Assert.Equal(InconsistencyKind.InsufficientLots, inconsistency.Kind);
        Assert.Equal(new Quantity(2m), inconsistency.MissingQuantity);
        Assert.Equal(2025, inconsistency.OccurredAt.InSourceTimeZone.Year);
        Assert.False(result.IsComplete);
    }

    [Fact]
    public void An_acquisition_fee_increases_the_lot_cost()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m, feeEuros: 12m)
            .Calculate();

        Assert.Equal(Money.Euros(1012m), Assert.Single(result.OpenLots).AcquisitionCost);
    }

    [Fact]
    public void A_disposal_fee_reduces_the_proceeds()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Sell("2025-02-01", quantity: 10m, grossEuros: 1400m, feeEuros: 15m)
            .Calculate();

        var realized = Assert.Single(result.RealizedResults);

        Assert.Equal(Money.Euros(1385m), realized.ProceedsInEuros);
        Assert.Equal(Money.Euros(385m), realized.ResultInEuros);
    }

    [Fact]
    public void A_disposal_fee_is_spread_across_the_consumed_lots_in_proportion()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Buy("2024-06-10", quantity: 10m, grossEuros: 1500m)
            .Sell("2025-02-01", quantity: 15m, grossEuros: 3000m, feeEuros: 30m)
            .Calculate();

        var realized = Assert.Single(result.RealizedResults);

        // 2970 netos: dos tercios al primer lote (10 de 15) y un tercio al segundo.
        Assert.Equal(Money.Euros(1980m), realized.ConsumedLots[0].ProceedsInEuros);
        Assert.Equal(Money.Euros(990m), realized.ConsumedLots[1].ProceedsInEuros);
        Assert.Equal(
            realized.ProceedsInEuros,
            realized.ConsumedLots.Aggregate(Money.Euros(0m), (total, lot) => total + lot.ProceedsInEuros));
    }

    [Fact]
    public void A_split_multiplies_earlier_lots_and_keeps_their_total_cost()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Split("2024-06-01", ratio: 4m)
            .Calculate();

        var lot = Assert.Single(result.OpenLots);

        Assert.Equal(new Quantity(40m), lot.RemainingQuantity);
        Assert.Equal(Money.Euros(1000m), lot.AcquisitionCost);
        Assert.Equal(Money.Euros(25m), lot.UnitCost);
    }

    [Fact]
    public void A_split_after_a_sale_does_not_change_that_sale_result()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Sell("2024-03-01", quantity: 4m, grossEuros: 600m)
            .Split("2024-06-01", ratio: 2m)
            .Calculate();

        var realized = Assert.Single(result.RealizedResults);

        Assert.Equal(Money.Euros(400m), realized.AcquisitionCostInEuros);
        Assert.Equal(Money.Euros(200m), realized.ResultInEuros);
        Assert.Equal(new Quantity(12m), Assert.Single(result.OpenLots).RemainingQuantity);
    }

    [Fact]
    public void A_split_without_ratio_reports_an_inconsistency()
    {
        var ledger = new Ledger().Buy("2024-01-10", quantity: 10m, grossEuros: 1000m);
        var result = FifoCalculator.Calculate(ledger.Owner, ledger.AssetId, WithRatioStripped(ledger));

        Assert.Equal(InconsistencyKind.SplitWithoutRatio, Assert.Single(result.Inconsistencies).Kind);
    }

    [Fact]
    public void A_dividend_does_not_touch_the_lots_and_keeps_gross_and_withholding_apart()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Dividend("2024-05-01", grossEuros: 50m, withholdingEuros: 9.5m)
            .Calculate();

        var income = Assert.Single(result.CapitalIncomes);

        Assert.Equal(Money.Euros(50m), income.GrossAmountInEuros);
        Assert.Equal(Money.Euros(9.5m), income.WithholdingInEuros);
        Assert.Equal(Money.Euros(40.5m), income.NetAmountInEuros);
        Assert.Equal(new Quantity(10m), Assert.Single(result.OpenLots).RemainingQuantity);
        Assert.Equal(Money.Euros(1000m), result.OpenLots[0].AcquisitionCost);
    }

    [Fact]
    public void Unclassified_and_pending_transfer_movements_are_left_out_and_counted()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Unclassified("2024-02-01")
            .PendingTransfer("2024-03-01", quantity: 2m, grossEuros: 300m)
            .Calculate();

        Assert.Equal(2, result.UnresolvedTransactionCount);
        Assert.False(result.IsComplete);
        Assert.Equal(new Quantity(10m), Assert.Single(result.OpenLots).RemainingQuantity);
    }

    [Fact]
    public void A_confirmed_internal_transfer_moves_the_lots_without_realising_a_result()
    {
        var ledger = new Ledger();

        var result = ledger
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .TransferOut("2024-06-01", quantity: 10m)
            .Calculate();

        var lot = Assert.Single(result.OpenLots);

        Assert.Empty(result.RealizedResults);
        Assert.Equal(ledger.OtherAccountId, lot.AccountId);
        Assert.Equal(Money.Euros(1000m), lot.AcquisitionCost);
        Assert.Equal(2024, lot.AcquiredAt.InSourceTimeZone.Year);
        Assert.Equal(1, lot.AcquiredAt.InSourceTimeZone.Month);
    }

    [Fact]
    public void A_partial_internal_transfer_splits_the_lot_and_keeps_both_halves_cost()
    {
        var ledger = new Ledger();

        var result = ledger
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .TransferOut("2024-06-01", quantity: 4m)
            .Calculate();

        Assert.Equal(2, result.OpenLots.Count);

        var moved = result.OpenLots.Single(lot => lot.AccountId == ledger.OtherAccountId);
        var kept = result.OpenLots.Single(lot => lot.AccountId == ledger.AccountId);

        Assert.Equal(Money.Euros(400m), moved.AcquisitionCost);
        Assert.Equal(Money.Euros(600m), kept.AcquisitionCost);
        Assert.Equal(Money.Euros(100m), moved.UnitCost);
        Assert.Equal(Money.Euros(100m), kept.UnitCost);
    }

    [Fact]
    public void A_transfer_network_fee_increases_the_cost_of_the_moved_lots()
    {
        var ledger = new Ledger();

        var result = ledger
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .TransferOut("2024-06-01", quantity: 10m, feeEuros: 3m)
            .Calculate();

        Assert.Equal(Money.Euros(1003m), Assert.Single(result.OpenLots).AcquisitionCost);
        Assert.Empty(result.RealizedResults);
    }

    [Fact]
    public void A_transfer_without_enough_quantity_in_the_source_account_reports_an_inconsistency()
    {
        var ledger = new Ledger();

        var result = ledger
            .Buy("2024-01-10", quantity: 2m, grossEuros: 200m)
            .TransferOut("2024-06-01", quantity: 5m)
            .Calculate();

        Assert.Equal(InconsistencyKind.InsufficientLotsForTransfer, Assert.Single(result.Inconsistencies).Kind);
    }

    [Fact]
    public void A_reward_is_capital_income_and_does_not_create_a_lot()
    {
        var result = new Ledger()
            .Reward("2024-04-01", quantity: 0.5m, grossEuros: 20m)
            .Calculate();

        Assert.Equal(Money.Euros(20m), Assert.Single(result.CapitalIncomes).GrossAmountInEuros);
        Assert.Empty(result.OpenLots);
    }

    [Fact]
    public void Amounts_not_converted_to_euros_are_rejected()
    {
        var ledger = new Ledger().Buy("2024-01-10", quantity: 1m, grossEuros: 100m);

        Assert.Throws<Kapea.Domain.Common.DomainException>(
            () => FifoCalculator.Calculate(ledger.Owner, ledger.AssetId, InDollars(ledger)));
    }

    private static IEnumerable<ValuedTransaction> WithRatioStripped(Ledger ledger) =>
        ledger.Split("2024-06-01", ratio: 2m).Transactions
            .Select(valued => valued.SplitRatio is null ? valued : valued with { SplitRatio = null });

    private static IEnumerable<ValuedTransaction> InDollars(Ledger ledger) =>
        ledger.Transactions.Select(valued => valued with
        {
            GrossAmountInEuros = new Money(valued.GrossAmountInEuros.Amount, Currency.FromCode("USD")),
        });
}
