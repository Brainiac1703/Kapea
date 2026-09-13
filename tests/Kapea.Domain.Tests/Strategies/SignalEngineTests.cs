using Kapea.Domain.Indicators;
using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Strategies;

public class SignalEngineTests
{
    private static readonly Guid Asset = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_entry_is_proposed_once_and_not_every_day_the_condition_holds()
    {
        // Un sistema cuya condición se cumple veinte días seguidos propone una compra,
        // no veinte.
        var series = Series([.. Enumerable.Repeat(200m, 30)]);

        var signals = SignalEngine.Run(Asset, series, AboveHundred());

        Assert.Single(signals, signal => signal.Direction == SignalDirection.Entry);
    }

    [Fact]
    public void The_signal_explains_which_condition_fired_and_with_what_values()
    {
        var series = Series([.. Enumerable.Repeat(200m, 5)]);

        var signal = SignalEngine.Run(Asset, series, AboveHundred())[0];

        Assert.Contains("precio", signal.Reason, StringComparison.Ordinal);
        Assert.Contains("200", signal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluating_the_whole_history_gives_the_same_signals_as_going_day_by_day()
    {
        // Es lo que garantiza que una señal de una fecha pasada sea la que se habría
        // emitido ese día, y lo que permite simular con este mismo motor.
        var prices = Enumerable.Range(0, 40).Select(index => 100m + (index % 7 * 10m)).ToArray();
        var series = Series(prices);
        var version = AboveHundred();

        var whole = SignalEngine.Run(Asset, series, version);

        var byDay = new List<Signal>();

        for (var day = 1; day <= series.Count; day++)
        {
            var upTo = SignalEngine.Run(Asset, [.. series.Take(day)], version);

            if (upTo.Count > byDay.Count)
            {
                byDay.AddRange(upTo.Skip(byDay.Count));
            }
        }

        Assert.Equal(
            [.. whole.Select(signal => (signal.Date, signal.Direction))],
            [.. byDay.Select(signal => (signal.Date, signal.Direction))]);
    }

    [Fact]
    public void The_entry_carries_its_target_and_its_exit_level()
    {
        var series = Series([.. Enumerable.Repeat(200m, 5)]);

        var signal = SignalEngine.Run(Asset, series, AboveHundred())[0];

        // Salida un diez por ciento por debajo, y objetivo al doble de esa distancia.
        Assert.Equal(Money.Euros(180m), signal.StopLoss);
        Assert.Equal(Money.Euros(240m), signal.Target);
    }

    [Fact]
    public void A_strategy_without_a_target_says_so_instead_of_inventing_one()
    {
        var version = StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            exit: Condition.When(Term.Close, Comparison.LessThan, Term.Of(50m)));

        var signal = SignalEngine.Run(Asset, Series([.. Enumerable.Repeat(200m, 5)]), version)[0];

        Assert.Null(signal.Target);
        Assert.Null(signal.StopLoss);
    }

    [Fact]
    public void Reaching_the_exit_level_closes_the_position()
    {
        // Cien días a doscientos y después una caída por debajo del nivel de salida.
        var series = Series([.. Enumerable.Repeat(200m, 3).Append(150m)]);

        var signals = SignalEngine.Run(Asset, series, AboveHundred());

        var exit = Assert.Single(signals, signal => signal.Direction == SignalDirection.Exit);

        Assert.Contains("nivel de salida", exit.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Reaching_the_target_closes_the_position()
    {
        var series = Series([.. Enumerable.Repeat(200m, 3).Append(250m)]);

        var exit = SignalEngine.Run(Asset, series, AboveHundred())
            .Single(signal => signal.Direction == SignalDirection.Exit);

        Assert.Contains("objetivo", exit.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void When_a_day_touches_both_levels_the_exit_level_wins()
    {
        // Con un cierre diario no se sabe qué se tocó primero. Dar por bueno el objetivo
        // sería contarse una historia favorable que no se puede sostener.
        var version = StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            stopLoss: new Level(LevelKind.Percentage, 0.5m),
            target: new Level(LevelKind.Percentage, 0.01m));

        var series = Series(200m, 90m);

        var exit = SignalEngine.Run(Asset, series, version)
            .Single(signal => signal.Direction == SignalDirection.Exit);

        Assert.Contains("nivel de salida", exit.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void No_signal_is_emitted_while_the_window_is_incomplete()
    {
        // Con menos días que la ventana, la media está a medias y la señal diría lo que
        // dijera el trozo que hay.
        var version = StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Indicator(Operand.SimpleMovingAverage, 20)),
            stopLoss: new Level(LevelKind.Percentage, 0.1m));

        Assert.Empty(SignalEngine.Run(Asset, Series([.. Enumerable.Repeat(200m, 10)]), version));
    }

    [Fact]
    public void A_crossing_is_not_the_same_as_a_state_that_was_already_there()
    {
        // Sin comparar con el día anterior, cada día por encima sería una señal.
        var version = StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.CrossesAbove, Term.Of(150m)),
            stopLoss: new Level(LevelKind.Percentage, 0.9m));

        var signals = SignalEngine.Run(Asset, Series(100m, 200m, 210m, 220m), version);

        var entry = Assert.Single(signals, signal => signal.Direction == SignalDirection.Entry);

        Assert.Equal(new DateOnly(2026, 1, 2), entry.Date);
    }

    private static StrategyVersion AboveHundred() =>
        StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            stopLoss: new Level(LevelKind.Percentage, 0.1m),
            target: new Level(LevelKind.RiskMultiple, 2m));

    private static List<PricePoint> Series(params decimal[] prices) =>
        [.. prices.Select((price, index) => new PricePoint(new DateOnly(2026, 1, 1).AddDays(index), price))];
}
