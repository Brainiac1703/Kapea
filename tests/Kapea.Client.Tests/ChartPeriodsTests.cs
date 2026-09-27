using Kapea.Client.Layout;

namespace Kapea.Client.Tests;

/// <summary>
/// La escala de periodos de las gráficas.
/// </summary>
public class ChartPeriodsTests
{
    private static readonly DateOnly Today = new(2026, 9, 27);

    [Fact]
    public void The_scale_goes_from_a_week_to_everything()
    {
        var scale = ChartPeriods.Scale(Today);

        Assert.Equal(9, scale.Count);
        Assert.Equal(7, scale[0].Days);
        Assert.Null(scale[^1].Days);
    }

    [Fact]
    public void The_periods_grow_in_order()
    {
        // Una escala desordenada se lee mal: los botones tienen que ir de menos a más.
        var days = ChartPeriods.Scale(Today).Select(period => period.Days).Where(days => days is not null).ToList();

        Assert.Equal([.. days.OrderBy(value => value)], days);
    }

    [Fact]
    public void There_is_no_jump_from_a_year_to_everything()
    {
        // Es lo que había: mirar meses obligaba a mirar años.
        var labels = ChartPeriods.Scale(Today).Select(period => period.Label).ToList();

        Assert.Contains("Evolution_Period_Quarter", labels);
        Assert.Contains("Evolution_Period_HalfYear", labels);
        Assert.Contains("Evolution_Period_TwoYears", labels);
    }

    [Fact]
    public void Year_to_date_depends_on_the_day()
    {
        var days = Days(Today, "Evolution_Period_YearToDate");

        Assert.Equal(269, days);
        Assert.NotEqual(days, Days(new DateOnly(2026, 3, 1), "Evolution_Period_YearToDate"));
    }

    [Fact]
    public void On_the_first_of_january_year_to_date_is_still_a_day()
    {
        // Cero días devolvería un rango vacío en lugar de la gráfica de hoy.
        Assert.Equal(1, Days(new DateOnly(2026, 1, 1), "Evolution_Period_YearToDate"));
    }

    [Fact]
    public void Every_period_has_a_text_of_its_own()
    {
        var labels = ChartPeriods.Scale(Today).Select(period => period.Label).ToList();

        Assert.Equal(labels.Count, labels.Distinct().Count());
        Assert.All(labels, label => Assert.StartsWith("Evolution_Period_", label, StringComparison.Ordinal));
    }

    [Fact]
    public void The_three_intervals_are_offered_in_the_order_they_grow() =>
        Assert.Equal(["Daily", "Weekly", "Monthly"], ChartPeriods.Intervals);

    [Fact]
    public void Each_interval_has_its_own_text() =>
        Assert.Equal("Evolution_Interval_Weekly", ChartPeriods.Label("Weekly"));

    private static int? Days(DateOnly today, string label) =>
        ChartPeriods.Scale(today).Single(period => period.Label == label).Days;
}
