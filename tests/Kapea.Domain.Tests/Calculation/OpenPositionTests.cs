using Kapea.Domain.Calculation;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class OpenPositionTests
{
    [Fact]
    public void A_position_with_a_market_price_reports_value_and_unrealised_result()
    {
        var asOf = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Buy("2024-06-10", quantity: 10m, grossEuros: 1500m)
            .Calculate();

        var position = OpenPosition.From(result.AssetId, result.Lots, Money.Euros(200m), asOf);

        Assert.NotNull(position);
        Assert.Equal(new Quantity(20m), position!.Quantity);
        Assert.Equal(Money.Euros(2500m), position.CostInEuros);
        Assert.Equal(Money.Euros(125m), position.AverageCostInEuros);
        Assert.Equal(Money.Euros(4000m), position.MarketValueInEuros);
        Assert.Equal(Money.Euros(1500m), position.UnrealisedResultInEuros);
        Assert.Equal(asOf, position.PriceAsOf);
    }

    [Fact]
    public void A_position_without_a_market_price_still_reports_quantity_and_average_cost()
    {
        var result = new Ledger().Buy("2024-01-10", quantity: 10m, grossEuros: 1000m).Calculate();

        var position = OpenPosition.From(result.AssetId, result.Lots);

        Assert.NotNull(position);
        Assert.Equal(new Quantity(10m), position!.Quantity);
        Assert.Equal(Money.Euros(100m), position.AverageCostInEuros);
        Assert.False(position.HasMarketPrice);
        Assert.Null(position.MarketValueInEuros);
        Assert.Null(position.UnrealisedResultInEuros);
        Assert.Null(position.PriceAsOf);
    }

    [Fact]
    public void An_asset_fully_sold_has_no_open_position()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Sell("2025-02-01", quantity: 10m, grossEuros: 1400m)
            .Calculate();

        Assert.Null(OpenPosition.From(result.AssetId, result.Lots));
        Assert.Single(result.RealizedResults);
    }

    [Fact]
    public void A_partially_sold_position_keeps_only_the_remaining_cost()
    {
        var result = new Ledger()
            .Buy("2024-01-10", quantity: 10m, grossEuros: 1000m)
            .Sell("2025-02-01", quantity: 4m, grossEuros: 800m)
            .Calculate();

        var position = OpenPosition.From(result.AssetId, result.Lots);

        Assert.Equal(new Quantity(6m), position!.Quantity);
        Assert.Equal(Money.Euros(600m), position.CostInEuros);
    }

    [Fact]
    public void A_price_without_an_instant_is_reported_without_one()
    {
        var result = new Ledger().Buy("2024-01-10", quantity: 10m, grossEuros: 1000m).Calculate();

        var position = OpenPosition.From(result.AssetId, result.Lots, Money.Euros(150m));

        Assert.True(position!.HasMarketPrice);
        Assert.Null(position.PriceAsOf);
    }
}
