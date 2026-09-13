using Kapea.Domain.Assets;
using Kapea.Domain.Calculation;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class PortfolioSummaryTests
{
    [Fact]
    public void Each_position_weighs_what_it_is_worth_over_the_total()
    {
        var summary = Build([
            Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m),
            Asset(AssetClass.Crypto, quantity: 1m, cost: 20m, price: 25m),
        ]);

        Assert.Equal([0.75m, 0.25m], [.. summary.Positions.Select(position => position.Weight)]);
        Assert.False(summary.WeightsArePartial);
    }

    [Fact]
    public void A_position_without_price_has_no_weight_and_leaves_the_rest_partial()
    {
        // El peso de lo que no se sabe cuánto vale no es cero. Repartir sobre el total
        // incluyéndolo daría pesos que no suman cien y que cambiarían solos en cuanto
        // apareciera el precio.
        var summary = Build([
            Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m),
            Asset(AssetClass.Crypto, quantity: 1m, cost: 20m, price: null),
        ]);

        Assert.Equal([1m, null], [.. summary.Positions.Select(position => position.Weight)]);
        Assert.True(summary.WeightsArePartial);
    }

    [Fact]
    public void The_position_carries_its_fees_and_what_it_already_realized()
    {
        var summary = Build([Asset(AssetClass.Equity, quantity: 10m, cost: 100m, price: 12m, fees: 9.95m, realized: 40m)]);

        var position = summary.Positions.Single();

        Assert.Equal(Money.Euros(9.95m), position.FeesInEuros);
        Assert.Equal(Money.Euros(40m), position.RealizedResultInEuros);
    }

    [Fact]
    public void What_was_sold_in_full_still_counts_in_the_accumulated_result()
    {
        // Un activo vendido entero no tiene posición abierta, así que su pérdida
        // desaparecería del acumulado justo cuando se materializa.
        var summary = Build(
            [Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m)],
            realized: -30.51m);

        Assert.Equal(Money.Euros(-30.51m), summary.Result.RealizedInEuros);
        Assert.Equal(Money.Euros(-5.51m), summary.Result.TotalInEuros);
    }

    [Fact]
    public void The_accumulated_result_adds_what_is_realized_to_what_is_latent()
    {
        var summary = Build([Asset(AssetClass.Equity, quantity: 10m, cost: 100m, price: 15m, realized: 40m)]);

        Assert.Equal(Money.Euros(40m), summary.Result.RealizedInEuros);
        Assert.Equal(Money.Euros(50m), summary.Result.UnrealisedInEuros);
        Assert.Equal(Money.Euros(90m), summary.Result.TotalInEuros);
    }

    [Fact]
    public void The_accumulated_realized_result_is_not_the_one_of_a_single_year()
    {
        // Los dos salen de las mismas transmisiones, pero el acumulado las suma todas y
        // el del ejercicio solo las de su año. Confundirlos declara de más o de menos.
        var summary = Build([Asset(AssetClass.Equity, quantity: 10m, cost: 100m, price: 10m, realized: 300m)]);

        var thisYear = Money.Euros(120m);

        Assert.NotEqual(thisYear, summary.Result.RealizedInEuros);
    }

    [Fact]
    public void Wealth_is_the_positions_plus_the_money_in_the_accounts()
    {
        var summary = Build(
            [Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m)],
            cash: new CashTotal(Money.Euros(1000m), []));

        Assert.Equal(Money.Euros(1075m), summary.Wealth.TotalInEuros);
        Assert.True(summary.Wealth.IsComplete);
    }

    [Fact]
    public void Wealth_missing_a_price_is_given_anyway_and_says_so()
    {
        var summary = Build(
            [
                Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m),
                Asset(AssetClass.Crypto, quantity: 1m, cost: 20m, price: null),
            ],
            cash: new CashTotal(Money.Euros(1000m), []));

        Assert.Equal(Money.Euros(1075m), summary.Wealth.TotalInEuros);
        Assert.True(summary.Wealth.MissingPrices);
        Assert.False(summary.Wealth.IsComplete);
    }

    [Fact]
    public void Wealth_missing_a_balance_says_so_too()
    {
        var summary = Build(
            [Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m)],
            cash: new CashTotal(Money.Euros(1000m), [Currency.FromCode("USD")]));

        Assert.True(summary.Wealth.MissingCash);
        Assert.False(summary.Wealth.IsComplete);
    }

    [Fact]
    public void An_unresolved_movement_also_leaves_the_cash_incomplete()
    {
        var summary = Build(
            [Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m)],
            cash: new CashTotal(Money.Euros(1000m), []),
            balances: new CashBalances([], UnresolvedMovements: 3));

        Assert.True(summary.Wealth.MissingCash);
    }

    [Fact]
    public void Each_class_is_a_group_with_its_own_subtotals()
    {
        var summary = Build([
            Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m),
            Asset(AssetClass.Equity, quantity: 10m, cost: 100m, price: 2.5m),
        ]);

        var crypto = summary.Groups.Single(group => group.Class == AssetClass.Crypto);
        var equity = summary.Groups.Single(group => group.Class == AssetClass.Equity);

        Assert.Equal(Money.Euros(75m), crypto.MarketValueInEuros);
        Assert.Equal(Money.Euros(25m), equity.MarketValueInEuros);
        Assert.Equal(0.75m, crypto.Weight);
        Assert.Equal(0.25m, equity.Weight);
    }

    [Fact]
    public void There_is_one_group_per_class_present_and_no_more()
    {
        // Los grupos salen de los datos y no de una lista escrita en la consulta, así
        // que una clase nueva en el catálogo aparecerá sola, sin tocar nada.
        var onlyCrypto = Build([
            Asset(AssetClass.Crypto, quantity: 1m, cost: 50m, price: 75m),
            Asset(AssetClass.Crypto, quantity: 2m, cost: 10m, price: 8m),
        ]);

        Assert.Equal(AssetClass.Crypto, Assert.Single(onlyCrypto.Groups).Class);
        Assert.Equal(2, Assert.Single(onlyCrypto.Groups).Positions.Count);
    }

    [Fact]
    public void The_income_collected_keeps_its_withholding_by_class()
    {
        var summary = Build(
            [Asset(AssetClass.Equity, quantity: 10m, cost: 100m, price: 12m)],
            income: [new IncomeByClass(AssetClass.Equity, Money.Euros(100m), Money.Euros(19m))]);

        var equity = summary.Income.Single();

        Assert.Equal(Money.Euros(100m), equity.GrossInEuros);
        Assert.Equal(Money.Euros(81m), equity.NetInEuros);
    }

    private static PortfolioSummary Build(
        IReadOnlyList<PortfolioAsset> assets,
        CashTotal? cash = null,
        CashBalances? balances = null,
        IReadOnlyList<IncomeByClass>? income = null,
        decimal? realized = null) =>
        PortfolioSummary.Build(
            assets,
            cash ?? new CashTotal(Money.Euros(0m), []),
            balances ?? new CashBalances([], UnresolvedMovements: 0),
            income ?? [],
            Money.Euros(realized ?? assets.Sum(asset => asset.RealizedResultInEuros.Amount)));

    private static PortfolioAsset Asset(
        AssetClass assetClass,
        decimal quantity,
        decimal cost,
        decimal? price,
        decimal fees = 0m,
        decimal realized = 0m) =>
        new(
            assetClass,
            new OpenPosition(
                Guid.NewGuid(),
                new Quantity(quantity),
                Money.Euros(cost),
                price is null ? null : Money.Euros(price.Value),
                price is null ? null : new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero)),
            Money.Euros(fees),
            Money.Euros(realized));
}
