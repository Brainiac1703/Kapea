using Kapea.Domain.Calculation;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

/// <summary>
/// Agrupar la serie en semanas o meses para poder mirar años.
/// </summary>
public class SeriesAggregationTests
{
    [Fact]
    public void Daily_leaves_the_series_untouched()
    {
        var days = Week();

        Assert.Same(days, SeriesAggregation.By(days, SeriesInterval.Daily));
    }

    [Fact]
    public void A_week_opens_with_the_first_and_closes_with_the_last()
    {
        var week = Assert.Single(SeriesAggregation.By(Week(), SeriesInterval.Weekly));

        Assert.Equal(Money.Euros(100m), week.OpenInEuros);
        Assert.Equal(Money.Euros(104m), week.PriceInEuros);
    }

    [Fact]
    public void The_high_of_a_stretch_is_the_highest_of_its_highs_and_not_of_its_closes()
    {
        // Es el error clásico al agregar velas: deja los extremos sistemáticamente por
        // debajo de la realidad.
        var week = Assert.Single(SeriesAggregation.By(Week(), SeriesInterval.Weekly));

        Assert.Equal(Money.Euros(130m), week.HighInEuros);
        Assert.Equal(Money.Euros(70m), week.LowInEuros);

        // El mayor de los cierres es 104: si saliera eso, la regla estaría rota.
        Assert.NotEqual(Money.Euros(104m), week.HighInEuros);
    }

    [Fact]
    public void A_stretch_with_closed_market_days_uses_the_ones_that_quoted()
    {
        var days = new[]
        {
            Day(1, close: 100m, high: 110m, low: 90m),
            Day(2, close: null),
            Day(3, close: 105m, high: 120m, low: 95m),
        };

        var week = Assert.Single(SeriesAggregation.By(days, SeriesInterval.Weekly));

        Assert.Equal(Money.Euros(105m), week.PriceInEuros);
        Assert.Equal(Money.Euros(120m), week.HighInEuros);
    }

    [Fact]
    public void A_stretch_with_no_data_at_all_is_not_invented()
    {
        var days = new[] { Day(1, close: null), Day(2, close: null) };

        Assert.Empty(SeriesAggregation.By(days, SeriesInterval.Weekly));
    }

    [Fact]
    public void A_day_without_a_range_contributes_its_close()
    {
        // Así un tramo con días de las dos clases sigue dando un extremo cierto.
        var days = new[]
        {
            Day(1, close: 100m, high: 110m, low: 90m),
            Day(2, close: 150m),
        };

        var week = Assert.Single(SeriesAggregation.By(days, SeriesInterval.Weekly));

        Assert.Equal(Money.Euros(150m), week.HighInEuros);
        Assert.Equal(Money.Euros(90m), week.LowInEuros);
    }

    [Fact]
    public void Weeks_start_on_monday()
    {
        // El 1 de junio de 2026 es lunes; el 7, domingo; el 8, el lunes siguiente.
        Assert.Equal(new DateOnly(2026, 6, 1), SeriesAggregation.Start(new DateOnly(2026, 6, 7), SeriesInterval.Weekly));
        Assert.Equal(new DateOnly(2026, 6, 8), SeriesAggregation.Start(new DateOnly(2026, 6, 8), SeriesInterval.Weekly));
    }

    [Fact]
    public void Two_weeks_give_two_points()
    {
        var days = new[]
        {
            Day(1, close: 100m, high: 110m, low: 90m),
            Day(8, close: 200m, high: 210m, low: 190m),
        };

        Assert.Equal(2, SeriesAggregation.By(days, SeriesInterval.Weekly).Count);
    }

    [Fact]
    public void A_month_groups_its_whole_calendar()
    {
        var days = new[]
        {
            Day(1, close: 100m, high: 110m, low: 90m),
            Day(8, close: 200m, high: 210m, low: 190m),
            Day(29, close: 150m, high: 160m, low: 140m),
        };

        var month = Assert.Single(SeriesAggregation.By(days, SeriesInterval.Monthly));

        Assert.Equal(Money.Euros(210m), month.HighInEuros);
        Assert.Equal(Money.Euros(150m), month.PriceInEuros);
        Assert.Equal(new DateOnly(2026, 6, 1), month.Date);
    }

    [Theory]
    [InlineData(30, SeriesInterval.Daily)]
    [InlineData(190, SeriesInterval.Daily)]
    [InlineData(365, SeriesInterval.Weekly)]
    [InlineData(760, SeriesInterval.Weekly)]
    [InlineData(1825, SeriesInterval.Monthly)]
    public void The_suggested_interval_grows_with_the_period(int days, SeriesInterval expected) =>
        Assert.Equal(expected, SeriesAggregation.Suggested(days));

    /// <summary>Una semana de lunes a domingo, con extremos por encima y por debajo de los cierres.</summary>
    private static AssetHistoryDay[] Week() =>
    [
        Day(1, close: 100m, high: 130m, low: 95m),
        Day(2, close: 101m, high: 105m, low: 70m),
        Day(5, close: 104m, high: 106m, low: 100m),
    ];

    private static AssetHistoryDay Day(int day, decimal? close, decimal? high = null, decimal? low = null) =>
        new(
            new DateOnly(2026, 6, day),
            Quantity.Zero,
            close is { } price ? Money.Euros(price) : null,
            null,
            null,
            false,
            close is { } open && high is not null ? Money.Euros(open) : null,
            high is { } top ? Money.Euros(top) : null,
            low is { } bottom ? Money.Euros(bottom) : null);
}
