using Kapea.Domain.Risk;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Strategies;

public class PositionSizingTests
{
    [Fact]
    public void An_asset_that_moves_more_gets_a_smaller_position()
    {
        // Arriesgar lo mismo en los dos es lo que hace comparables las decisiones.
        var calm = PositionSizing.For(Money.Euros(10_000m), 0.01m, Money.Euros(100m), Money.Euros(95m));
        var wild = PositionSizing.For(Money.Euros(10_000m), 0.01m, Money.Euros(100m), Money.Euros(90m));

        Assert.True(wild!.AmountInEuros.Amount < calm!.AmountInEuros.Amount);
        Assert.Equal(calm.RiskInEuros, wild.RiskInEuros);
    }

    [Fact]
    public void The_risk_is_what_is_lost_if_the_exit_level_is_reached()
    {
        var size = PositionSizing.For(Money.Euros(10_000m), 0.02m, Money.Euros(100m), Money.Euros(90m));

        Assert.Equal(Money.Euros(200m), size!.RiskInEuros);

        // Doscientos euros de riesgo entre diez de distancia son veinte unidades.
        Assert.Equal(new Quantity(20m), size.Quantity);
    }

    [Fact]
    public void Without_an_exit_level_nothing_is_proposed()
    {
        // Suponerlo sería inventar el riesgo, que es justamente el dato que hace falta.
        Assert.Null(PositionSizing.For(Money.Euros(10_000m), 0.01m, Money.Euros(100m), null));
    }

    [Fact]
    public void An_exit_level_above_the_entry_is_rejected() =>
        Assert.Null(PositionSizing.For(Money.Euros(10_000m), 0.01m, Money.Euros(100m), Money.Euros(110m)));

    [Fact]
    public void A_position_over_its_cap_is_flagged_with_what_would_fit()
    {
        var size = PositionSizing.For(
            Money.Euros(10_000m), 0.05m, Money.Euros(100m), Money.Euros(99m), positionCap: 0.2m);

        Assert.True(size!.ExceedsPositionCap);
        Assert.Equal(Money.Euros(2_000m), size.AllowedByCap);
    }

    [Fact]
    public void A_position_within_its_cap_is_not_flagged()
    {
        var size = PositionSizing.For(
            Money.Euros(10_000m), 0.01m, Money.Euros(100m), Money.Euros(90m), positionCap: 0.2m);

        Assert.False(size!.ExceedsPositionCap);
    }
}

public class ConcentrationTests
{
    [Fact]
    public void A_few_assets_over_the_threshold_are_reported()
    {
        var warning = Concentration.Check(Weights(("BTC", 0.3m), ("ETH", 0.3m), ("XRP", 0.2m), ("SOL", 0.2m)), 3, 0.7m);

        Assert.NotNull(warning);
        Assert.Equal(0.8m, warning.Share);
        Assert.Equal(["BTC", "ETH", "XRP"], [.. warning.Top.Select(weight => weight.Name)]);
    }

    [Fact]
    public void A_spread_portfolio_produces_no_warning() =>
        Assert.Null(Concentration.Check(
            Weights(("A", 0.25m), ("B", 0.25m), ("C", 0.25m), ("D", 0.25m)), 2, 0.7m));

    [Fact]
    public void An_empty_portfolio_produces_no_warning() =>
        Assert.Null(Concentration.Check([], 3, 0.5m));

    [Fact]
    public void A_weight_outside_its_band_proposes_what_to_move()
    {
        var proposals = Concentration.Rebalance(
            Weights(("BTC", 0.6m), ("ETH", 0.4m)),
            new Dictionary<string, decimal> { ["BTC"] = 0.5m, ["ETH"] = 0.5m },
            band: 0.05m,
            total: Money.Euros(1000m));

        Assert.Equal(2, proposals.Count);

        // Sobra un diez por ciento de bitcoin: cien euros que habría que recortar.
        Assert.Equal(Money.Euros(-100m), proposals.Single(p => p.Name == "BTC").Adjustment);
        Assert.Equal(Money.Euros(100m), proposals.Single(p => p.Name == "ETH").Adjustment);
    }

    [Fact]
    public void A_deviation_within_the_band_proposes_nothing()
    {
        // Rebalancear por cualquier desviación genera operaciones que solo pagan comisiones.
        var proposals = Concentration.Rebalance(
            Weights(("BTC", 0.52m), ("ETH", 0.48m)),
            new Dictionary<string, decimal> { ["BTC"] = 0.5m, ["ETH"] = 0.5m },
            band: 0.05m,
            total: Money.Euros(1000m));

        Assert.Empty(proposals);
    }

    [Fact]
    public void An_asset_without_a_target_is_left_alone()
    {
        var proposals = Concentration.Rebalance(
            Weights(("BTC", 0.9m), ("ETH", 0.1m)),
            new Dictionary<string, decimal> { ["ETH"] = 0.5m },
            band: 0.05m,
            total: Money.Euros(1000m));

        Assert.Equal("ETH", Assert.Single(proposals).Name);
    }

    private static List<Weight> Weights(params (string Name, decimal Share)[] weights) =>
        [.. weights.Select(weight => new Weight(
            weight.Name, Money.Euros(weight.Share * 1000m), weight.Share))];
}
