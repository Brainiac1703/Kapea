using Kapea.Client.Components;

namespace Kapea.Client.Tests;

/// <summary>
/// La reducción de puntos para dibujar.
/// </summary>
/// <remarks>
/// Reduce lo que se pinta y nada más. Lo que se calcula y lo que se lee al pasar el
/// ratón salen de la serie entera, así que una media no puede cambiar porque la gráfica
/// dibuje menos puntos.
/// </remarks>
public class ChartSamplingTests
{
    [Fact]
    public void A_series_that_fits_is_left_exactly_as_it_is()
    {
        var points = Series(500);

        Assert.Same(points, ChartSampling.Reduce(points, 1_000));
    }

    [Fact]
    public void A_series_that_does_not_fit_is_reduced_to_the_maximum()
    {
        var reduced = ChartSampling.Reduce(Series(9_764), 1_000);

        Assert.InRange(reduced.Count, 1, 1_000);
    }

    [Fact]
    public void The_reduced_series_keeps_the_extremes()
    {
        var points = Series(9_764);

        var reduced = ChartSampling.Reduce(points, 1_000);

        // Sin el primero, la gráfica empezaría después de donde dice el eje.
        Assert.Equal(points[0].Date, reduced[0].Date);
        Assert.True(reduced[^1].Date <= points[^1].Date);
    }

    [Fact]
    public void The_reduced_series_keeps_its_order()
    {
        var reduced = ChartSampling.Reduce(Series(5_000), 1_000);

        Assert.Equal([.. reduced.OrderBy(point => point.Date)], reduced);
    }

    [Fact]
    public void A_gap_of_one_day_disappears_when_years_are_shown()
    {
        // Un fin de semana suelto no puede partir la línea cuando cada punto dibujado
        // representa una semana: se vería cortada de arriba abajo sin que falte nada.
        var points = Series(7_000, without: date => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        var reduced = ChartSampling.Reduce(points, 1_000);

        Assert.All(reduced, point => Assert.NotNull(point.Value));
    }

    [Fact]
    public void A_stretch_with_no_value_at_all_stays_a_gap()
    {
        // Setenta días sin cotización es lo que le pasa de verdad a AVAX en 2020. Eso sí
        // tiene que verse cortado: rellenarlo dibujaría una recta que nadie cotizó.
        var start = new DateOnly(2000, 1, 3).AddDays(3_000);
        var points = Series(7_000, without: date => date >= start && date < start.AddDays(70));

        var reduced = ChartSampling.Reduce(points, 1_000);

        Assert.Contains(reduced, point => point.Value is null);
    }

    [Fact]
    public void Reducing_does_not_invent_values()
    {
        var points = Series(9_764);
        var valores = points.Select(point => point.Value).ToHashSet();

        var reduced = ChartSampling.Reduce(points, 1_000);

        Assert.All(reduced, point => Assert.Contains(point.Value, valores));
    }

    private static IReadOnlyList<ChartPoint> Series(int days, Func<DateOnly, bool>? without = null)
    {
        var first = new DateOnly(2000, 1, 3);

        return
        [
            .. Enumerable.Range(0, days).Select(day =>
            {
                var date = first.AddDays(day);

                return new ChartPoint(date, without?.Invoke(date) == true ? null : 100m + day);
            }),
        ];
    }
}
