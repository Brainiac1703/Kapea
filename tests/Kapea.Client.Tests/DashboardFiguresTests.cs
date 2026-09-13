using System.Globalization;
using Kapea.Client.Components;
using Kapea.Client.Layout;
using Kapea.Client.Models;
using Kapea.Client.Services;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Tests;

public class DashboardFiguresTests
{
    [Fact]
    public void The_last_day_compares_with_the_previous_priced_day()
    {
        var change = DashboardFigures.LastDay(
        [
            Day(1, 1000m),
            Day(2, 1050m),
        ]);

        Assert.Equal(50m, change!.Amount);
        Assert.Equal(0.05m, change.Rate);
    }

    [Fact]
    public void Money_put_in_that_day_is_not_a_gain()
    {
        // Comprar cien euros sube el valor cien euros, y eso no es haber ganado nada.
        var change = DashboardFigures.LastDay(
        [
            Day(1, 1000m),
            Day(2, 1110m, contribution: 100m),
        ]);

        Assert.Equal(10m, change!.Amount);
    }

    [Fact]
    public void A_day_without_prices_is_skipped_rather_than_compared()
    {
        // El día 2 vale de menos porque le falta un precio: compararlo sería inventarse
        // una caída y una subida que no ocurrieron.
        var change = DashboardFigures.LastDay(
        [
            Day(1, 1000m),
            Day(2, 400m, complete: false),
            Day(3, 1020m),
        ]);

        Assert.Equal(20m, change!.Amount);
        Assert.Equal(new DateOnly(2026, 9, 3), change.Date);
    }

    [Fact]
    public void The_money_put_in_on_a_skipped_day_still_counts()
    {
        var change = DashboardFigures.LastDay(
        [
            Day(1, 1000m),
            Day(2, 400m, complete: false, contribution: 50m),
            Day(3, 1070m),
        ]);

        Assert.Equal(20m, change!.Amount);
    }

    [Fact]
    public void With_a_single_priced_day_there_is_nothing_to_compare() =>
        Assert.Null(DashboardFigures.LastDay([Day(1, 1000m), Day(2, 900m, complete: false)]));

    [Fact]
    public void An_empty_portfolio_has_no_rate() =>
        Assert.Null(DashboardFigures.LastDay([Day(1, 0m), Day(2, 100m, contribution: 100m)])!.Rate);

    private static PortfolioHistoryDayResponse Day(int day, decimal value, bool complete = true, decimal contribution = 0m) =>
        new(new DateOnly(2026, 9, day), value, contribution, complete);
}

public class BrandingTests
{
    [Theory]
    [InlineData("Nacho Tovar", "NT")]
    [InlineData("nacho", "N")]
    [InlineData("  María  José  García ", "MJ")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void The_avatar_shows_up_to_two_initials(string? name, string expected) =>
        Assert.Equal(expected, MainLayout.Initials(name));

    [Fact]
    public void A_gain_carries_its_plus_sign()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");

        try
        {
            Assert.StartsWith("+", Format.SignedEuros(48.12m), StringComparison.Ordinal);
            Assert.StartsWith("-", Format.SignedEuros(-48.12m), StringComparison.Ordinal);
            Assert.DoesNotContain("+", Format.SignedEuros(0m), StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Series_beyond_the_checked_palette_fold_into_other()
    {
        // Repetir un color haría que dos líneas distintas se leyeran como la misma.
        Assert.Equal("series-0", ChartLine.SeriesAt(0));
        Assert.Equal("series-4", ChartLine.SeriesAt(ChartLine.DistinctSeries - 1));
        Assert.Equal("series-other", ChartLine.SeriesAt(ChartLine.DistinctSeries));
    }
}
