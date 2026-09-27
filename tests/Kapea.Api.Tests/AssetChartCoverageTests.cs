using System.Net.Http.Json;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

/// <summary>
/// La gráfica de un activo, que existe aunque el activo no se tenga.
/// </summary>
/// <remarks>
/// El precio salía de la posición de cada día, así que desaparecía con ella: la pantalla
/// quedaba vacía para todo lo que sólo se vigila, que es justo donde hace falta para
/// decidir si entrar.
/// </remarks>
[Collection(ApiCollection.Name)]
public class AssetChartCoverageTests(KapeaApiFactory factory)
{
    private static readonly DateOnly First = new(2026, 3, 1);

    [Fact]
    public async Task An_asset_never_bought_has_its_quotes()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "NEVERHELD", days: 5);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-05&window=2");

        Assert.Equal(5, history!.Days.Count);
        Assert.All(history.Days, day => Assert.NotNull(day.PriceInEuros));

        // Sin posición no hay ni cantidad ni valor, y eso no es lo mismo que no tener precio.
        Assert.All(history.Days, day => Assert.Equal(0m, day.Quantity));
        Assert.All(history.Days, day => Assert.Null(day.ValueInEuros));

        // Y con precios sí hay indicadores, que es para lo que sirve la pantalla.
        Assert.NotEmpty(history.SimpleMovingAverage);
    }

    [Fact]
    public async Task A_day_without_a_quote_stays_without_a_price()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "WITHHOLE", days: 5, skip: 3);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-05");

        var hole = history!.Days.Single(day => day.Date == new DateOnly(2026, 3, 3));

        Assert.Null(hole.PriceInEuros);
        Assert.Equal(4, history.Days.Count(day => day.PriceInEuros is not null));
    }

    [Fact]
    public async Task Everything_available_is_asked_for_without_knowing_since_when()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "LONGONE", days: 40);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?to=2026-04-09&all=true");

        // Sin all sólo llegarían los del rango por omisión; con él, desde la primera
        // cotización guardada, que depende del activo y no de un número de días.
        Assert.Equal(First, history!.Days[0].Date);
        Assert.Equal(40, history.Days.Count(day => day.PriceInEuros is not null));
    }

    [Fact]
    public async Task A_range_starting_before_the_first_quote_is_not_an_error()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "YOUNGONE", days: 3);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-02-20&to=2026-03-03");

        Assert.Equal(new DateOnly(2026, 2, 20), history!.Days[0].Date);
        Assert.Null(history.Days[0].PriceInEuros);
        Assert.Equal(3, history.Days.Count(day => day.PriceInEuros is not null));
    }

    [Fact]
    public async Task Looking_at_a_chart_does_not_change_the_portfolio()
    {
        // La cotización se pide aparte de lo que reconstruye la cartera. Si alimentara
        // esa reconstrucción, mirar un gráfico movería el patrimonio.
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "UNRELATED", days: 5);

        var before = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");

        await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-05");

        var after = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");

        Assert.Equal(before!.WealthInEuros, after!.WealthInEuros);
        Assert.Equal(before.TotalCostInEuros, after.TotalCostInEuros);
        Assert.Equal(before.Groups.Count, after.Groups.Count);
    }

    [Fact]
    public async Task The_range_of_each_day_comes_with_the_series()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "WITHRANGE", days: 5, withRange: true);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-05");

        Assert.True(history!.HasRange);
        Assert.All(history.Days, day => Assert.NotNull(day.HighInEuros));
    }

    [Fact]
    public async Task An_asset_whose_provider_gives_no_range_says_so()
    {
        // POL, PEPE y TAO sólo los cubre CoinGecko. La pantalla tiene que llevarlo bien
        // en lugar de parecer rota.
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "NORANGE", days: 5);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-05");

        Assert.False(history!.HasRange);
        Assert.All(history.Days, day => Assert.Null(day.HighInEuros));
    }

    [Fact]
    public async Task The_series_can_be_grouped_by_weeks()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "BYWEEK", days: 21, withRange: true);

        var daily = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-21&interval=daily");
        var weekly = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-21&interval=weekly");

        Assert.Equal("Daily", daily!.Interval);
        Assert.Equal("Weekly", weekly!.Interval);
        Assert.True(weekly.Days.Count < daily.Days.Count);

        // Agrupar para mirar no puede cambiar lo que el motor calcula: los indicadores
        // salen de los días, no de los tramos. Una media de veinte semanas no es la
        // misma que una de veinte días.
        Assert.Equal(daily.SimpleMovingAverage, weekly.SimpleMovingAverage);
        Assert.Equal(daily.RelativeStrengthIndex, weekly.RelativeStrengthIndex);
        Assert.Equal(daily.Dispersion?.AverageRange, weekly.Dispersion?.AverageRange);
    }

    [Fact]
    public async Task An_unknown_interval_is_refused_instead_of_silently_ignored()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "BADINTV", days: 5);

        var response = await client.GetAsync(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-05&interval=hourly");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_long_period_is_grouped_without_being_asked()
    {
        // Cinco años en días son más puntos que píxeles. El periodo sugiere el intervalo.
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "LONGSPAN", days: 40);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2020-03-01&to=2026-03-05");

        Assert.Equal("Monthly", history!.Interval);
    }

    [Fact]
    public async Task The_dispersion_comes_with_the_window_it_was_measured_over()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "DISPER", days: 40, withRange: true);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-04-09&window=5&interval=daily");

        var dispersion = history!.Dispersion;

        Assert.NotNull(dispersion);
        Assert.Equal(5, dispersion.WindowDays);
        Assert.NotEmpty(dispersion.Volatility);
        Assert.NotEmpty(dispersion.AverageRange);
        Assert.True(dispersion.AverageRangeFromDayRange);
    }

    [Fact]
    public async Task Too_few_days_give_no_dispersion_instead_of_a_weaker_one()
    {
        // Una banda calculada sobre menos días parecería igual de firme y no lo sería.
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "TOOSHORT", days: 3, withRange: true);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-03-03&window=20&interval=daily");

        Assert.Null(history!.Dispersion);
    }

    [Fact]
    public async Task Without_a_range_the_average_says_it_was_measured_on_closes()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var asset = await QuotedAssetAsync(user, "CLOSEONLY", days: 40);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{asset}?from=2026-03-01&to=2026-04-09&window=5&interval=daily");

        Assert.False(history!.Dispersion!.AverageRangeFromDayRange);
    }

    /// <summary>Un activo del catálogo con cotizaciones y sin un solo movimiento.</summary>
    private async Task<Guid> QuotedAssetAsync(
        Guid user, string symbol, int days, int? skip = null, bool withRange = false)
    {
        await using var context = factory.CreateContext(user);

        var asset = Asset.Create(symbol, AssetClass.Crypto);
        context.Assets.Add(asset);

        for (var day = 0; day < days; day++)
        {
            var date = First.AddDays(day);

            if (skip is { } hole && date.Day == hole)
            {
                continue;
            }

            var price = new DailyPrice(asset.Id, date, 100m + day, "Prueba");

            context.DailyPrices.Add(withRange
                ? price.WithRange(open: 99m + day, high: 105m + day, low: 98m + day)
                : price);
        }

        await context.SaveChangesAsync();

        return asset.Id;
    }
}
