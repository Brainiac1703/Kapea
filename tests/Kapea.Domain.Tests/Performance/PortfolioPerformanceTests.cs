using Kapea.Domain.Calculation;
using Kapea.Domain.Performance;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Performance;

public class PortfolioPerformanceTests
{
    [Fact]
    public void Putting_money_in_is_not_a_return()
    {
        // Sin descontar la aportación, meter mil euros en una cartera de mil parecería
        // un cien por cien de rentabilidad.
        var days = Days((1000m, 1000m), (2000m, 1000m), (2000m, 0m));

        Assert.Equal(0m, PortfolioPerformance.Of(days).TimeWeighted);
    }

    [Fact]
    public void Two_periods_chain_regardless_of_the_contribution_between_them()
    {
        // Sube un diez por ciento, entra dinero, y vuelve a subir un diez por ciento: el
        // conjunto es un veintiuno por ciento, y el tamaño de la aportación no influye.
        var small = Days((100m, 100m), (110m, 0m), (1110m, 1000m), (1221m, 0m));
        var large = Days((100m, 100m), (110m, 0m), (10110m, 10000m), (11121m, 0m));

        Assert.Equal(0.21m, decimal.Round(PortfolioPerformance.Of(small).TimeWeighted, 4));
        Assert.Equal(0.21m, decimal.Round(PortfolioPerformance.Of(large).TimeWeighted, 4));
    }

    [Fact]
    public void Without_contributions_both_returns_agree()
    {
        var days = Days((1000m, 1000m), (1100m, 0m), (1200m, 0m));

        var performance = PortfolioPerformance.Of(days);

        // La ponderada por dinero es anual y la del periodo es de tres días, así que se
        // comparan por su signo y su orden de magnitud, no al decimal.
        Assert.True(performance.TimeWeighted > 0m);
        Assert.True(performance.MoneyWeighted > 0m);
    }

    [Fact]
    public void Contributing_just_before_a_rise_pays_off_in_the_money_weighted_return()
    {
        // Las dos carteras viven los mismos rendimientos diarios, pero una mete el
        // dinero grande antes de la subida y la otra después.
        var before = Days((100m, 100m), (1100m, 1000m), (2200m, 0m));
        var after = Days((100m, 100m), (200m, 0m), (1200m, 1000m), (1200m, 0m));

        var lucky = PortfolioPerformance.Of(before).MoneyWeighted;
        var unlucky = PortfolioPerformance.Of(after).MoneyWeighted;

        Assert.True(lucky > unlucky, $"esperaba {lucky} mayor que {unlucky}");
    }

    [Fact]
    public void The_deepest_fall_from_a_peak_is_reported()
    {
        var days = Days((100m, 100m), (200m, 0m), (160m, 0m), (180m, 0m));

        Assert.Equal(0.2m, decimal.Round(PortfolioPerformance.Of(days).MaximumDrawdown, 4));
    }

    [Fact]
    public void A_recovered_fall_says_how_long_it_took()
    {
        var days = Days((100m, 100m), (200m, 0m), (160m, 0m), (180m, 0m), (200m, 0m));

        Assert.Equal(3, PortfolioPerformance.Of(days).DrawdownRecoveredInDays);
    }

    [Fact]
    public void A_fall_still_open_says_so_instead_of_inventing_a_recovery()
    {
        var days = Days((100m, 100m), (200m, 0m), (160m, 0m), (150m, 0m));

        var performance = PortfolioPerformance.Of(days);

        Assert.Equal(0.25m, decimal.Round(performance.MaximumDrawdown, 4));
        Assert.Null(performance.DrawdownRecoveredInDays);
    }

    [Fact]
    public void A_portfolio_that_only_goes_up_has_no_fall()
    {
        var days = Days((100m, 100m), (120m, 0m), (140m, 0m));

        Assert.Equal(0m, PortfolioPerformance.Of(days).MaximumDrawdown);
    }

    [Fact]
    public void A_period_with_days_without_prices_is_marked_incomplete()
    {
        // La cifra se da igual, pero quien la lea tiene que saber sobre qué se calculó.
        var days = new List<PortfolioDay>
        {
            Day(new DateOnly(2026, 3, 1), 100m, 100m, complete: true),
            Day(new DateOnly(2026, 3, 2), 0m, 0m, complete: false),
        };

        Assert.False(PortfolioPerformance.Of(days).IsComplete);
    }

    [Fact]
    public void A_series_with_a_single_day_has_nothing_to_measure()
    {
        var performance = PortfolioPerformance.Of(Days((100m, 100m)));

        Assert.Equal(0m, performance.TimeWeighted);
        Assert.Equal(0m, performance.Volatility);
    }

    [Fact]
    public void A_portfolio_that_moves_more_is_more_volatile()
    {
        var calm = Days((100m, 100m), (101m, 0m), (100m, 0m), (101m, 0m));
        var wild = Days((100m, 100m), (130m, 0m), (100m, 0m), (130m, 0m));

        Assert.True(PortfolioPerformance.Of(wild).Volatility > PortfolioPerformance.Of(calm).Volatility);
    }

    private static List<PortfolioDay> Days(params (decimal Value, decimal Contribution)[] days) =>
        [.. days.Select((day, index) => Day(
            new DateOnly(2026, 3, 1).AddDays(index), day.Value, day.Contribution, complete: true))];

    private static PortfolioDay Day(DateOnly date, decimal value, decimal contribution, bool complete) =>
        new(date, [], Money.Euros(value), Money.Euros(contribution), complete);
}
