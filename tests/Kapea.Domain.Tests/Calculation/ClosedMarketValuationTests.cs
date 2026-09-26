using Kapea.Domain.Assets;
using Kapea.Domain.Calculation;
using Kapea.Domain.MarketData;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

/// <summary>
/// Valorar una posición los días que su mercado no cotizó.
/// </summary>
/// <remarks>
/// Un sábado, una acción vale lo que valía el viernes al cierre. El mercado no cotizó,
/// pero la posición no dejó de valer: sin esto el patrimonio se desploma cada fin de
/// semana y la línea se corta.
/// </remarks>
public class ClosedMarketValuationTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Share = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid Coin = Guid.NewGuid();

    [Fact]
    public void A_weekend_is_valued_at_the_last_close_and_is_not_incomplete()
    {
        // Jueves y viernes cotiza, sábado y domingo no, lunes vuelve.
        var days = Build(Equity(Share, (2, 100m), (3, 110m), (6, 120m)), Equity(Other, (2, 1m), (3, 1m), (6, 1m)));

        var saturday = days.Single(day => day.Date == Day(4));

        Assert.True(saturday.IsComplete);
        Assert.True(saturday.HasCarriedPrices);

        var held = saturday.Assets.Single(asset => asset.AssetId == Share);

        Assert.Equal(Money.Euros(110m), held.PriceInEuros);
        Assert.Equal(Day(3), held.CarriedFrom);
        Assert.True(held.IsCarried);
    }

    [Fact]
    public void The_total_does_not_fall_over_a_weekend()
    {
        // Era el síntoma: 3.527 € el jueves, 3.167 € el viernes, 3.661 € el lunes.
        var days = Build(Equity(Share, (2, 100m), (3, 110m), (6, 120m)), Equity(Other, (2, 1m), (3, 1m), (6, 1m)));

        var friday = days.Single(day => day.Date == Day(3)).ValueInEuros;

        Assert.Equal(friday, days.Single(day => day.Date == Day(4)).ValueInEuros);
        Assert.Equal(friday, days.Single(day => day.Date == Day(5)).ValueInEuros);
    }

    [Fact]
    public void A_real_gap_still_leaves_the_day_incomplete()
    {
        // Sólo a una le falta el día 3, así que la bolsa estaba abierta y es una laguna.
        var days = Build(
            Equity(Share, (2, 100m), (4, 120m)),
            Equity(Other, (2, 1m), (3, 1m), (4, 1m)));

        var gap = days.Single(day => day.Date == Day(3));

        Assert.False(gap.IsComplete);
        Assert.False(gap.HasCarriedPrices);
        Assert.Null(gap.Assets.Single(asset => asset.AssetId == Share).PriceInEuros);
    }

    [Fact]
    public void A_holiday_in_one_market_does_not_carry_the_other()
    {
        var days = Build(
            Equity(Share, (2, 100m), (6, 120m)),
            Equity(Other, (2, 1m), (6, 1m)),
            Crypto(Coin, (2, 10m), (3, 11m), (4, 12m), (5, 13m), (6, 14m)));

        var saturday = days.Single(day => day.Date == Day(4));

        Assert.True(saturday.Assets.Single(asset => asset.AssetId == Share).IsCarried);
        Assert.False(saturday.Assets.Single(asset => asset.AssetId == Coin).IsCarried);
        Assert.Equal(Money.Euros(12m), saturday.Assets.Single(asset => asset.AssetId == Coin).PriceInEuros);
    }

    [Fact]
    public void Nothing_is_carried_when_there_is_no_earlier_close()
    {
        // El primer día del rango cae en mercado cerrado y no hay cierre anterior.
        var days = Build(Equity(Share, (6, 120m)), Equity(Other, (6, 1m)));

        var first = days[0];

        Assert.False(first.IsComplete);
        Assert.Null(first.Assets.Single(asset => asset.AssetId == Share).PriceInEuros);
    }

    [Fact]
    public void A_close_from_before_the_range_can_be_carried()
    {
        // Sin mirar antes del rango, un rango que empieza en sábado saldría incompleto.
        var days = PortfolioHistory.Build(
            [Buy(Share, 1, 2m), Buy(Other, 1, 1m)],
            Merge(Equity(Share, (6, 120m)), Equity(Other, (6, 1m))),
            Day(2),
            Day(6),
            Classes(),
            new Dictionary<Guid, DailyPrice>
            {
                [Share] = new(Share, Day(1), 90m, "Prueba"),
                [Other] = new(Other, Day(1), 1m, "Prueba"),
            });

        var first = days[0];

        Assert.True(first.IsComplete);
        Assert.Equal(Day(1), first.Assets.Single(asset => asset.AssetId == Share).CarriedFrom);
    }

    [Fact]
    public void Without_classes_the_series_behaves_as_before()
    {
        var days = PortfolioHistory.Build(
            [Buy(Share, 1, 2m)],
            Merge(Equity(Share, (2, 100m), (3, 110m), (6, 120m))),
            Day(2),
            Day(6));

        Assert.False(days.Single(day => day.Date == Day(4)).IsComplete);
    }

    private static IReadOnlyList<PortfolioDay> Build(
        params Dictionary<Guid, IReadOnlyList<DailyPrice>>[] prices) =>
        PortfolioHistory.Build(
            [.. prices.SelectMany(entry => entry.Keys).Distinct().Select(asset => Buy(asset, 1, 2m))],
            Merge(prices),
            Day(2),
            Day(6),
            Classes());

    private static Dictionary<Guid, string> Classes() => new()
    {
        [Share] = "Equity:US",
        [Other] = "Equity:US",
        [Coin] = "Crypto",
    };

    private static Dictionary<Guid, IReadOnlyList<DailyPrice>> Merge(
        params Dictionary<Guid, IReadOnlyList<DailyPrice>>[] prices) =>
        prices.SelectMany(entry => entry).ToDictionary(entry => entry.Key, entry => entry.Value);

    private static Dictionary<Guid, IReadOnlyList<DailyPrice>> Equity(
        Guid assetId, params (int Day, decimal Price)[] daily) => Series(assetId, daily);

    private static Dictionary<Guid, IReadOnlyList<DailyPrice>> Crypto(
        Guid assetId, params (int Day, decimal Price)[] daily) => Series(assetId, daily);

    private static Dictionary<Guid, IReadOnlyList<DailyPrice>> Series(
        Guid assetId, (int Day, decimal Price)[] daily) =>
        new()
        {
            [assetId] = [.. daily.Select(entry => new DailyPrice(assetId, Day(entry.Day), entry.Price, "Prueba"))],
        };

    private static DateOnly Day(int day) => new(2026, 7, day);

    private static ValuedTransaction Buy(Guid assetId, int on, decimal quantity) =>
        new(
            Transaction.Imported(
                Owner,
                Guid.NewGuid(),
                TransactionType.Buy,
                assetId,
                new Quantity(quantity),
                null,
                Money.Euros(100m),
                Money.Euros(0m),
                Occurrence.FromOffset(new DateTimeOffset(Day(on), TimeOnly.MinValue, TimeSpan.Zero), "UTC"),
                TransactionSource.FromImport(
                    Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString())),
            Money.Euros(100m),
            Money.Euros(0m));
}
