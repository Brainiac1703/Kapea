using System.Net;
using Kapea.Application.Abstractions;
using Kapea.Infrastructure.MarketData;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.MarketData;

public class CoinGeckoMarketPriceProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Prices_are_read_with_the_instant_the_platform_reports()
    {
        var (provider, handler) = Create(new RecordedResponseHandler()
            .RespondWithFile(Path.Combine("MarketData", "Recorded", "simple-price.json")));

        var prices = await provider.GetPricesAsync(["BTC", "ETH"]);

        Assert.Equal(2, prices.Count);
        Assert.Equal(58234.12m, prices["BTC"].PriceInEuros);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1757332800), prices["BTC"].AsOf);
        Assert.Contains("bitcoin", handler.Requests.Single().RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("vs_currencies=eur", handler.Requests.Single().RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Symbols_the_provider_does_not_cover_simply_do_not_come_back()
    {
        var (provider, handler) = Create(new RecordedResponseHandler()
            .RespondWithFile(Path.Combine("MarketData", "Recorded", "simple-price.json")));

        var prices = await provider.GetPricesAsync(["BTC", "ETH", "SAN.ES"]);

        Assert.False(prices.ContainsKey("SAN.ES"));
        Assert.DoesNotContain("SAN", handler.Requests.Single().RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asking_only_for_uncovered_symbols_does_not_call_the_platform()
    {
        var (provider, handler) = Create(new RecordedResponseHandler());

        Assert.Empty(await provider.GetPricesAsync(["SAN.ES", "AAPL.US"]));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_unavailable_platform_degrades_to_no_prices()
    {
        // Sin precio la cartera sigue enseñando cantidad y coste medio; fallar aquí
        // dejaría sin responder una consulta que no depende del precio.
        var (provider, _) = Create(new RecordedResponseHandler().RespondWithStatus(HttpStatusCode.TooManyRequests));

        Assert.Empty(await provider.GetPricesAsync(["BTC"]));
    }

    [Fact]
    public async Task A_malformed_response_degrades_to_no_prices()
    {
        var (provider, _) = Create(new RecordedResponseHandler().RespondWithContent("no es json"));

        Assert.Empty(await provider.GetPricesAsync(["BTC"]));
    }

    [Fact]
    public async Task A_coin_without_a_euro_price_is_skipped()
    {
        var (provider, _) = Create(new RecordedResponseHandler()
            .RespondWithContent("""{"bitcoin":{"usd":63000.0},"ethereum":{"eur":2410.55}}"""));

        var prices = await provider.GetPricesAsync(["BTC", "ETH"]);

        Assert.False(prices.ContainsKey("BTC"));
        Assert.True(prices.ContainsKey("ETH"));
    }

    [Fact]
    public async Task A_price_without_an_instant_falls_back_to_the_current_time()
    {
        var (provider, _) = Create(new RecordedResponseHandler().RespondWithContent("""{"bitcoin":{"eur":58234.12}}"""));

        var prices = await provider.GetPricesAsync(["BTC"]);

        Assert.Equal(Now, prices["BTC"].AsOf);
    }

    private static (IMarketPriceProvider Provider, RecordedResponseHandler Handler) Create(RecordedResponseHandler handler) =>
        (new CoinGeckoMarketPriceProvider(
                new HttpClient(handler) { BaseAddress = new Uri("https://api.coingecko.com/") },
                new FakeTimeProvider(Now),
                NullLogger<CoinGeckoMarketPriceProvider>.Instance),
            handler);
}
