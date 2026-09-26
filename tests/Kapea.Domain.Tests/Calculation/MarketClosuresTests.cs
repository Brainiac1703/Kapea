using Kapea.Domain.Calculation;
using Kapea.Domain.MarketData;

namespace Kapea.Domain.Tests.Calculation;

/// <summary>
/// Deducir qué días no cotizó un mercado mirando qué activos tienen precio.
/// </summary>
public class MarketClosuresTests
{
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();
    private static readonly Guid Third = Guid.NewGuid();

    private const string Wall = "Equity:US";
    private const string Frankfurt = "Equity:DE";
    private const string Coins = "Crypto";

    [Fact]
    public void A_day_no_asset_of_the_class_quoted_is_a_closed_market()
    {
        // El 4 de julio de 2026 cayó en sábado y el viernes 3 cerró la bolsa
        // estadounidense por la fiesta. Ninguna acción cotizó ninguno de los dos.
        var closures = Equities(
            (First, [2, 6]),
            (Second, [2, 6]));

        Assert.True(closures.WasClosed(Wall, Day(3)));
        Assert.True(closures.WasClosed(Wall, Day(4)));
        Assert.False(closures.WasClosed(Wall, Day(2)));
        Assert.False(closures.WasClosed(Wall, Day(6)));
    }

    [Fact]
    public void A_day_only_one_asset_missed_is_a_gap_and_not_a_closed_market()
    {
        // Si las demás cotizaron, la bolsa estaba abierta y a ésta le falta el dato.
        var closures = Equities(
            (First, [2, 3, 4]),
            (Second, [2, 4]));

        Assert.False(closures.WasClosed(Wall, Day(3)));
    }

    [Fact]
    public void With_a_single_asset_of_a_class_nothing_is_deduced()
    {
        // Es el límite conocido: sin otro con qué contrastar, cualquier laguna pasaría
        // por festivo. Se prefiere tratarlo como lo que era, un dato que falta.
        var closures = Equities((First, [2, 6]));

        Assert.False(closures.WasClosed(Wall, Day(3)));
        Assert.False(closures.WasClosed(Wall, Day(4)));
    }

    [Fact]
    public void Crypto_never_closes_because_it_always_quotes()
    {
        var closures = MarketClosures.Of(
            Prices((First, Coins, [2, 3, 4, 5, 6]), (Second, Coins, [2, 3, 4, 5, 6])),
            new Dictionary<Guid, string> { [First] = Coins, [Second] = Coins },
            Day(2),
            Day(6));

        Assert.False(closures.WasClosed(Coins, Day(4)));
    }

    [Fact]
    public void One_class_closing_says_nothing_about_another()
    {
        var closures = MarketClosures.Of(
            Prices(
                (First, Wall, [2, 6]),
                (Second, Wall, [2, 6]),
                (Third, Coins, [2, 3, 4, 5, 6])),
            new Dictionary<Guid, string>
            {
                [First] = Wall,
                [Second] = Wall,
                [Third] = Coins,
            },
            Day(2),
            Day(6));

        Assert.True(closures.WasClosed(Wall, Day(4)));
        Assert.False(closures.WasClosed(Coins, Day(4)));
    }

    [Fact]
    public void An_asset_the_provider_does_not_cover_at_all_says_nothing()
    {
        // Uno sin ninguna cotización no dice si la bolsa abrió, así que no cuenta para
        // llegar a los dos que hacen fiable la deducción.
        var closures = Equities(
            (First, [2, 6]),
            (Second, []));

        Assert.False(closures.WasClosed(Wall, Day(4)));
    }

    [Fact]
    public void A_holiday_in_one_exchange_does_not_affect_another()
    {
        // El 3 de julio de 2026 cerró la bolsa estadounidense por la fiesta del 4 y la
        // alemana operó. Mirando toda la renta variable junta, el día parecía abierto.
        var closures = MarketClosures.Of(
            Prices(
                (First, Wall, [2, 6]),
                (Second, Wall, [2, 6]),
                (Third, Frankfurt, [2, 3, 6])),
            new Dictionary<Guid, string> { [First] = Wall, [Second] = Wall, [Third] = Frankfurt },
            Day(2),
            Day(6));

        Assert.True(closures.WasClosed(Wall, Day(3)));
        Assert.False(closures.WasClosed(Frankfurt, Day(3)));
    }

    [Fact]
    public void Without_classes_nothing_is_closed()
    {
        Assert.False(MarketClosures.None.WasClosed(Wall, Day(4)));
    }

    private static MarketClosures Equities(params (Guid Asset, int[] Days)[] assets) =>
        MarketClosures.Of(
            Prices([.. assets.Select(asset => (asset.Asset, Wall, asset.Days))]),
            assets.ToDictionary(asset => asset.Asset, _ => Wall),
            Day(2),
            Day(6));

    private static Dictionary<Guid, IReadOnlyList<DailyPrice>> Prices(
        params (Guid Asset, string Market, int[] Days)[] assets) =>
        assets.ToDictionary(
            asset => asset.Asset,
            asset => (IReadOnlyList<DailyPrice>)
                [.. asset.Days.Select(day => new DailyPrice(asset.Asset, Day(day), 100m, "Prueba"))]);

    private static DateOnly Day(int day) => new(2026, 7, day);
}
