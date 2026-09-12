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

public class MacdTests
{
    [Fact]
    public void A_series_shorter_than_the_slow_window_gives_no_value() =>
        Assert.Empty(TechnicalIndicators.Macd(Rising(8), fastDays: 3, slowDays: 8, signalDays: 2));

    [Fact]
    public void The_line_is_the_distance_between_the_two_averages()
    {
        // En una serie que solo sube, la media rápida va por delante de la lenta, así que
        // la línea es positiva.
        var macd = TechnicalIndicators.Macd(Rising(40), fastDays: 3, slowDays: 8, signalDays: 2);

        Assert.NotEmpty(macd);
        Assert.All(macd, point => Assert.True(point.Line > 0m));
    }

    [Fact]
    public void The_distance_changes_sign_when_the_line_crosses_its_signal()
    {
        // Baja veinte días y sube veinte. En el giro, la línea pasa de un lado a otro de
        // su señal, y eso es lo que se mira de este indicador.
        var prices = Enumerable.Range(0, 20).Select(index => 120m - index)
            .Concat(Enumerable.Range(0, 20).Select(index => 100m + index))
            .ToArray();

        var macd = TechnicalIndicators.Macd(Series(prices), fastDays: 3, slowDays: 8, signalDays: 3);
        var signs = macd.Select(point => Math.Sign(point.Distance)).Distinct().ToList();

        Assert.True(signs.Count > 1, string.Join(", ", macd.Select(point => point.Distance.ToString("0.###"))));
    }

    [Fact]
    public void Each_value_keeps_its_date()
    {
        var macd = TechnicalIndicators.Macd(Rising(40), fastDays: 3, slowDays: 8, signalDays: 2);

        Assert.Equal([.. macd.Select(point => point.Date).Order()], [.. macd.Select(point => point.Date)]);
    }

    [Fact]
    public void A_fast_window_that_is_not_faster_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TechnicalIndicators.Macd(Rising(40), fastDays: 8, slowDays: 8));

    private static List<PricePoint> Rising(int days) =>
        Series([.. Enumerable.Range(0, days).Select(index => 100m + index)]);

    private static List<PricePoint> Series(params decimal[] prices) =>
        [.. prices.Select((price, index) => new PricePoint(new DateOnly(2026, 1, 1).AddDays(index), price))];
}

public class BollingerBandsTests
{
    [Fact]
    public void On_a_flat_series_the_bands_meet_the_average()
    {
        var bands = TechnicalIndicators.BollingerBands(Series(100m, 100m, 100m, 100m), days: 3);

        Assert.All(bands, point => Assert.Equal(point.Middle, point.Upper));
        Assert.All(bands, point => Assert.Equal(point.Middle, point.Lower));
    }

    [Fact]
    public void The_bands_open_when_the_series_moves()
    {
        var bands = TechnicalIndicators.BollingerBands(Series(100m, 120m, 80m, 130m), days: 3);

        Assert.All(bands, point => Assert.True(point.Upper > point.Lower));
    }

    [Fact]
    public void A_price_above_the_upper_band_is_visible_in_that_day()
    {
        // Nueve días planos y una subida del diez por ciento. Con la ventana ya asentada
        // el salto queda fuera de la banda; con pocos días, el propio salto la ensancha
        // lo bastante para contenerse a sí mismo.
        var series = Series(100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 110m);

        var bands = TechnicalIndicators.BollingerBands(series, days: 10);

        Assert.True(series[^1].PriceInEuros > bands[^1].Upper);
    }

    [Fact]
    public void There_is_no_value_before_the_window_is_complete()
    {
        var bands = TechnicalIndicators.BollingerBands(Series(100m, 110m, 120m, 130m), days: 3);

        Assert.Equal(2, bands.Count);
        Assert.Equal(new DateOnly(2026, 1, 3), bands[0].Date);
    }

    private static List<PricePoint> Series(params decimal[] prices) =>
        [.. prices.Select((price, index) => new PricePoint(new DateOnly(2026, 1, 1).AddDays(index), price))];
}

public class AverageDailyRangeTests
{
    [Fact]
    public void An_asset_that_moves_more_has_a_larger_range()
    {
        var calm = TechnicalIndicators.AverageDailyRange(Alternating(100m, 101m, 20), days: 5);
        var wild = TechnicalIndicators.AverageDailyRange(Alternating(100m, 130m, 20), days: 5);

        Assert.True(wild[^1].Value > calm[^1].Value);
    }

    [Fact]
    public void A_series_with_a_single_day_has_nothing_to_measure() =>
        Assert.Empty(TechnicalIndicators.AverageDailyRange(
            [new PricePoint(new DateOnly(2026, 1, 1), 100m)], days: 5));

    [Fact]
    public void A_flat_series_does_not_move_at_all()
    {
        var range = TechnicalIndicators.AverageDailyRange(Alternating(100m, 100m, 20), days: 5);

        Assert.All(range, point => Assert.Equal(0m, point.Value));
    }

    [Fact]
    public void The_first_value_arrives_one_day_after_the_window()
    {
        // Cada día aporta una variación contra el anterior, así que la primera ventana de
        // cinco variaciones necesita seis días.
        var range = TechnicalIndicators.AverageDailyRange(Alternating(100m, 110m, 10), days: 5);

        Assert.Equal(new DateOnly(2026, 1, 6), range[0].Date);
    }

    private static List<PricePoint> Alternating(decimal low, decimal high, int days) =>
    [
        .. Enumerable.Range(0, days).Select(index => new PricePoint(
            new DateOnly(2026, 1, 1).AddDays(index),
            index % 2 == 0 ? low : high)),
    ];
}
