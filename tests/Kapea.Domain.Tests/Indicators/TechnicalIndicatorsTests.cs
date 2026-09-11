using Kapea.Domain.Indicators;

namespace Kapea.Domain.Tests.Indicators;

public class TechnicalIndicatorsTests
{
    [Fact]
    public void A_window_longer_than_the_series_gives_no_value()
    {
        // Promediar lo que hay daría una media de cinco días llamándola de veinte.
        Assert.Empty(TechnicalIndicators.SimpleMovingAverage(Series(1, 2, 3), 20));
    }

    [Fact]
    public void The_simple_average_starts_when_the_window_is_complete()
    {
        var average = TechnicalIndicators.SimpleMovingAverage(Series(10, 20, 30, 40), 3);

        Assert.Equal(2, average.Count);
        Assert.Equal(20m, average[0].Value);
        Assert.Equal(30m, average[1].Value);
    }

    [Fact]
    public void Each_value_keeps_the_day_it_belongs_to()
    {
        var average = TechnicalIndicators.SimpleMovingAverage(Series(10, 20, 30, 40), 3);

        Assert.Equal(new DateOnly(2026, 3, 3), average[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 4), average[1].Date);
    }

    [Fact]
    public void The_indicator_does_not_fill_the_days_that_are_missing()
    {
        // La serie salta del 2 al 5. Si el indicador rellenara esos días, el valor
        // llevaría una fecha que no le corresponde y dejaría de cuadrar con la gráfica.
        var series = new List<PricePoint>
        {
            new(new DateOnly(2026, 3, 1), 10m),
            new(new DateOnly(2026, 3, 2), 20m),
            new(new DateOnly(2026, 3, 5), 30m),
        };

        var average = TechnicalIndicators.SimpleMovingAverage(series, 3);

        Assert.Equal(new DateOnly(2026, 3, 5), Assert.Single(average).Date);
        Assert.Equal(20m, average[0].Value);
    }

    [Fact]
    public void The_exponential_average_starts_from_the_simple_one()
    {
        var average = TechnicalIndicators.ExponentialMovingAverage(Series(10, 20, 30, 40), 3);

        Assert.Equal(20m, average[0].Value);

        // Suavizado de 2/(3+1) = 0,5: la mitad del camino entre 20 y 40.
        Assert.Equal(30m, average[1].Value);
    }

    [Fact]
    public void A_series_that_only_goes_up_has_a_strength_of_one_hundred()
    {
        var rsi = TechnicalIndicators.RelativeStrengthIndex(Series(10, 20, 30, 40, 50), 4);

        Assert.Equal(100m, Assert.Single(rsi).Value);
    }

    [Fact]
    public void A_series_that_only_goes_down_has_a_strength_of_zero()
    {
        var rsi = TechnicalIndicators.RelativeStrengthIndex(Series(50, 40, 30, 20, 10), 4);

        Assert.Equal(0m, Assert.Single(rsi).Value);
    }

    [Fact]
    public void A_series_that_rises_and_falls_the_same_sits_in_the_middle()
    {
        // Dos subidas de diez y dos bajadas de diez: ganancias y pérdidas medias
        // iguales, así que la fuerza relativa es cincuenta.
        var rsi = TechnicalIndicators.RelativeStrengthIndex(Series(10, 20, 10, 20, 10), 4);

        Assert.Equal(50m, Assert.Single(rsi).Value);
    }

    [Fact]
    public void The_strength_needs_one_more_day_than_the_window()
    {
        // El primer día no tiene variación con la que compararse.
        Assert.Empty(TechnicalIndicators.RelativeStrengthIndex(Series(10, 20, 30, 40), 4));
        Assert.Single(TechnicalIndicators.RelativeStrengthIndex(Series(10, 20, 30, 40, 50), 4));
    }

    [Fact]
    public void A_window_of_less_than_two_days_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TechnicalIndicators.SimpleMovingAverage(Series(10, 20), 1));

    private static List<PricePoint> Series(params decimal[] prices) =>
        [.. prices.Select((price, index) => new PricePoint(new DateOnly(2026, 3, index + 1), price))];
}
