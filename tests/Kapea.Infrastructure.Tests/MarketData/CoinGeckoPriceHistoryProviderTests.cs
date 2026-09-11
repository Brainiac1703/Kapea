using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Infrastructure.MarketData;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.MarketData;

public class CoinGeckoPriceHistoryProviderTests
{
    private static readonly Guid Asset = Guid.NewGuid();
    private static readonly DateTimeOffset Today = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_close_of_each_day_is_its_last_point()
    {
        // Con rangos cortos CoinGecko devuelve puntos horarios. Quedarse con el primero
        // daría el precio de la madrugada en lugar del cierre.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("coingecko-history-btc.json"));

        var prices = await Provider(handler).GetHistoryAsync(Request(new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 25)));

        Assert.NotEmpty(prices);
        Assert.Equal(prices.Select(price => price.Date).Distinct().Count(), prices.Count);
        Assert.All(prices, price => Assert.Equal("CoinGecko", price.Source));
    }

    [Fact]
    public async Task The_series_comes_back_in_order_and_within_the_range()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("coingecko-history-btc.json"));

        var prices = await Provider(handler).GetHistoryAsync(Request(new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 24)));

        Assert.Equal([.. prices.Select(price => price.Date).Order()], [.. prices.Select(price => price.Date)]);
        Assert.All(prices, price => Assert.InRange(price.Date, new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 24)));
    }

    [Fact]
    public async Task A_range_older_than_the_free_window_is_not_even_asked_for()
    {
        // La capa gratuita solo sirve los últimos 365 días. Gastar la petición para
        // recibir un error no ayuda a nadie, y no es un fallo: es falta de cobertura.
        var handler = new RecordedResponseHandler();

        var prices = await Provider(handler).GetHistoryAsync(
            Request(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 1)));

        Assert.Empty(prices);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_range_that_starts_too_far_back_is_trimmed_to_what_can_be_served()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("coingecko-history-btc.json"));

        await Provider(handler).GetHistoryAsync(Request(new DateOnly(2024, 1, 1), new DateOnly(2026, 8, 25)));

        var query = Assert.Single(handler.Requests).RequestUri!.Query;
        var earliest = new DateTimeOffset(new DateTime(2025, 9, 13), TimeSpan.Zero).ToUnixTimeSeconds();

        Assert.Contains($"from={earliest}", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_asset_coingecko_does_not_know_leaves_the_series_empty()
    {
        var handler = new RecordedResponseHandler();

        Assert.Empty(await Provider(handler).GetHistoryAsync(
            Request(new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 25), "INVENTADO")));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_provider_that_fails_leaves_the_series_empty()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(System.Net.HttpStatusCode.TooManyRequests);

        Assert.Empty(await Provider(handler).GetHistoryAsync(
            Request(new DateOnly(2026, 8, 23), new DateOnly(2026, 8, 25))));
    }

    private static string Recorded(string fileName) => Path.Combine("MarketData", "Recorded", fileName);

    private static PriceHistoryRequest Request(DateOnly from, DateOnly to, string symbol = "BTC") =>
        new(Asset, symbol, AssetClass.Crypto, from, to);

    private static CoinGeckoPriceHistoryProvider Provider(HttpMessageHandler handler)
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(Today);

        return new CoinGeckoPriceHistoryProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo/") },
            clock,
            NullLogger<CoinGeckoPriceHistoryProvider>.Instance);
    }
}
