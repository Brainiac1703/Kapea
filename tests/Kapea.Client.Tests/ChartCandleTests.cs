using Kapea.Client.Components;

namespace Kapea.Client.Tests;

/// <summary>
/// El recorrido de un tramo, tal y como lo recibe la gráfica.
/// </summary>
public class ChartCandleTests
{
    [Fact]
    public void A_candle_that_closes_above_its_open_rose() =>
        Assert.True(Candle(open: 100m, close: 110m).Rose);

    [Fact]
    public void A_candle_that_closes_below_its_open_did_not() =>
        Assert.False(Candle(open: 110m, close: 100m).Rose);

    [Fact]
    public void A_flat_candle_counts_as_risen()
    {
        // Tiene que caer de un lado: pintarla de un tercer color para un día en que no
        // pasó nada sería ruido.
        Assert.True(Candle(open: 100m, close: 100m).Rose);
    }

    private static ChartCandle Candle(decimal open, decimal close) =>
        new(new DateOnly(2026, 6, 1), open, Math.Max(open, close) + 5m, Math.Min(open, close) - 5m, close);
}
