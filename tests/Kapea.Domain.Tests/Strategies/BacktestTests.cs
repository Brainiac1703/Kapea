using Kapea.Domain.Indicators;
using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Strategies;

public class BacktestTests
{
    private static readonly Guid Asset = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_purchase_never_uses_the_price_of_the_day_of_the_signal()
    {
        // La señal nace del cierre, así que comprar a ese mismo cierre es comprar a un
        // precio que ya no existía cuando se supo. Es el error que más infla un simulador.
        var series = Series(90m, 200m, 300m, 400m);

        var result = Backtest.Run(Asset, series, AboveHundred(), Money.Euros(1000m), TradingCost.None, SavingsTax.None);

        var trade = Assert.Single(result.Trades);

        // La condición se cumple el segundo día, a doscientos; la compra ocurre el tercero.
        Assert.Equal(new DateOnly(2026, 1, 3), trade.EntryDate);
        Assert.Equal(Money.Euros(300m), trade.EntryPrice);
    }

    [Fact]
    public void The_commission_inside_the_price_comes_out_of_the_result()
    {
        var series = Series(90m, 200m, 200m, 200m);
        var cost = new TradingCost(0.0095m, Money.Euros(0m));

        var result = Backtest.Run(Asset, series, AboveHundred(), Money.Euros(1000m), cost, SavingsTax.None);

        // Compra de mil euros con un 0,95 % dentro: nueve euros y medio de comisión.
        Assert.Equal(Money.Euros(9.5m), Assert.Single(result.Trades).Fees);
    }

    [Fact]
    public void A_fixed_commission_is_charged_as_well()
    {
        var series = Series(90m, 200m, 200m);
        var cost = new TradingCost(0m, Money.Euros(2m));

        var result = Backtest.Run(Asset, series, AboveHundred(), Money.Euros(1000m), cost, SavingsTax.None);

        Assert.Equal(Money.Euros(2m), Assert.Single(result.Trades).Fees);
    }

    [Fact]
    public void A_system_that_trades_a_lot_shows_what_went_in_commissions()
    {
        // Con casi un dos por ciento por operación completa, hay sistemas inviables por
        // aritmética, y el simulador tiene que decirlo.
        var prices = Enumerable.Range(0, 40).Select(index => index % 2 == 0 ? 90m : 200m).ToArray();
        var cost = new TradingCost(0.0095m, Money.Euros(0m));

        var result = Backtest.Run(Asset, Series(prices), Oscillating(), Money.Euros(1000m), cost, SavingsTax.None);

        Assert.True(result.Trades.Count > 1);
        Assert.True(result.Fees.Amount > 0m);
    }

    [Fact]
    public void A_realized_gain_is_shown_before_and_after_tax()
    {
        var result = Backtest.Run(
            Asset, Series(90m, 200m, 200m, 400m, 400m), TakeProfit(), Money.Euros(1000m),
            TradingCost.None, SavingsTax.Spain2026);

        Assert.True(result.Result.Amount > 0m);
        Assert.True(result.ResultAfterTax.Amount < result.Result.Amount);
        Assert.True(result.Tax.Amount > 0m);
    }

    [Fact]
    public void An_unrealized_gain_pays_no_tax_yet()
    {
        // La posición sigue abierta: la ganancia no se ha materializado y no tributa.
        var result = Backtest.Run(
            Asset, Series(90m, 200m, 200m, 400m), AboveHundred(), Money.Euros(1000m),
            TradingCost.None, SavingsTax.Spain2026);

        Assert.True(result.Result.Amount > 0m);
        Assert.Equal(Money.Euros(0m), result.Tax);
    }

    [Fact]
    public void A_loss_pays_no_tax()
    {
        var series = Series(90m, 200m, 200m, 100m);

        var result = Backtest.Run(
            Asset, series, AboveHundred(), Money.Euros(1000m), TradingCost.None, SavingsTax.Spain2026);

        Assert.Equal(Money.Euros(0m), result.Tax);
    }

    [Fact]
    public void The_tax_brackets_are_data_and_change_the_figure()
    {
        var series = Series(90m, 200m, 200m, 400m, 400m);

        var taxed = Backtest.Run(
            Asset, series, TakeProfit(), Money.Euros(1000m), TradingCost.None, SavingsTax.Spain2026);
        var untaxed = Backtest.Run(
            Asset, series, TakeProfit(), Money.Euros(1000m), TradingCost.None, SavingsTax.None);

        Assert.NotEqual(taxed.ResultAfterTax, untaxed.ResultAfterTax);
    }

    [Fact]
    public void A_system_worse_than_doing_nothing_says_so()
    {
        // Entra tarde y sale en la caída, mientras que no tocar nada habría aguantado.
        var series = Series(90m, 200m, 200m, 150m, 400m);

        var result = Backtest.Run(
            Asset, series, AboveHundred(), Money.Euros(1000m), TradingCost.None, SavingsTax.None);

        Assert.False(result.BeatsBuyAndHold);
    }

    [Fact]
    public void The_detail_says_how_many_trades_closed_and_how_many_won()
    {
        var prices = Enumerable.Range(0, 40).Select(index => index % 2 == 0 ? 90m : 200m).ToArray();

        var result = Backtest.Run(
            Asset, Series(prices), Oscillating(), Money.Euros(1000m), TradingCost.None, SavingsTax.None);

        Assert.True(result.Closed > 0);
        Assert.True(result.Winners <= result.Closed);
        Assert.All(result.Trades, trade => Assert.NotEmpty(trade.EntryReason));
    }

    [Fact]
    public void A_position_still_open_is_valued_at_the_last_close_without_charging_a_sale()
    {
        var series = Series(90m, 200m, 200m, 220m);
        var cost = new TradingCost(0.01m, Money.Euros(0m));

        var trade = Assert.Single(
            Backtest.Run(Asset, series, AboveHundred(), Money.Euros(1000m), cost, SavingsTax.None).Trades);

        Assert.False(trade.IsClosed);

        // Solo la comisión de compra: la de venta no se ha pagado.
        Assert.Equal(Money.Euros(10m), trade.Fees);
    }

    private static StrategyVersion AboveHundred() =>
        StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            exit: Condition.When(Term.Close, Comparison.LessThan, Term.Of(100m)));

    /// <summary>Entra por encima de cien y recoge beneficio por encima de trescientos cincuenta.</summary>
    private static StrategyVersion TakeProfit() =>
        StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            exit: Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(350m)));

    private static StrategyVersion Oscillating() =>
        StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.CrossesAbove, Term.Of(150m)),
            exit: Condition.When(Term.Close, Comparison.CrossesBelow, Term.Of(150m)));

    private static List<PricePoint> Series(params decimal[] prices) =>
        [.. prices.Select((price, index) => new PricePoint(new DateOnly(2026, 1, 1).AddDays(index), price))];
}
