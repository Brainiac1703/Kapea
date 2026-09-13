using Kapea.Domain.Calculation;
using Kapea.Domain.MarketData;
using Kapea.Domain.Performance;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Performance;

public class BenchmarkComparisonTests
{
    private static readonly Guid Reference = Guid.NewGuid();

    [Fact]
    public void The_reference_receives_the_same_money_on_the_same_days()
    {
        // Comparar contra el rendimiento pelado de un índice sería tramposo: no recibió
        // el dinero cuando lo recibió la cartera.
        var portfolio = Portfolio((1000m, 1000m), (1000m, 0m), (1000m, 0m));
        var prices = Prices(100m, 100m, 200m);

        var benchmark = BenchmarkComparison.Of(portfolio, prices);

        // Diez unidades compradas a cien, que al final valen doscientas cada una.
        Assert.Equal(Money.Euros(2000m), benchmark.Days[^1].ValueInEuros);
        Assert.Equal(Money.Euros(1000m), benchmark.Contributed);
    }

    [Fact]
    public void A_later_contribution_buys_at_the_price_of_its_day()
    {
        var portfolio = Portfolio((100m, 100m), (100m, 0m), (300m, 200m));
        var prices = Prices(100m, 100m, 200m);

        var benchmark = BenchmarkComparison.Of(portfolio, prices);

        // Una unidad a cien más una a doscientos: dos unidades que valen cuatrocientos.
        Assert.Equal(Money.Euros(400m), benchmark.Days[^1].ValueInEuros);
    }

    [Fact]
    public void A_day_without_a_price_leaves_the_comparison_incomplete()
    {
        // Rellenar el hueco haría parecer buena o mala una gestión por falta de datos.
        var portfolio = Portfolio((1000m, 1000m), (1000m, 0m), (1000m, 0m));

        var benchmark = BenchmarkComparison.Of(portfolio, [Price(0, 100m), Price(2, 200m)]);

        Assert.False(benchmark.IsComplete);
        Assert.False(benchmark.Days[1].IsComplete);
    }

    [Fact]
    public void A_reference_with_no_prices_at_all_is_incomplete_and_worth_nothing()
    {
        var benchmark = BenchmarkComparison.Of(Portfolio((1000m, 1000m), (1000m, 0m)), []);

        Assert.False(benchmark.IsComplete);
        Assert.All(benchmark.Days, day => Assert.Equal(Money.Euros(0m), day.ValueInEuros));
    }

    [Fact]
    public void A_withdrawal_sells_part_of_the_reference()
    {
        var portfolio = Portfolio((1000m, 1000m), (500m, -500m));
        var prices = Prices(100m, 100m);

        var benchmark = BenchmarkComparison.Of(portfolio, prices);

        Assert.Equal(Money.Euros(500m), benchmark.Days[^1].ValueInEuros);
    }

    private static List<PortfolioDay> Portfolio(params (decimal Value, decimal Contribution)[] days) =>
        [.. days.Select((day, index) => new PortfolioDay(
            Day(index), [], Money.Euros(day.Value), Money.Euros(day.Contribution), true))];

    private static List<DailyPrice> Prices(params decimal[] prices) =>
        [.. prices.Select((price, index) => Price(index, price))];

    private static DailyPrice Price(int index, decimal price) => new(Reference, Day(index), price, "Prueba");

    private static DateOnly Day(int index) => new DateOnly(2026, 3, 1).AddDays(index);
}
