using System.Net;
using System.Text;
using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Infrastructure.Import.Kraken;
using Kapea.Infrastructure.MarketData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.MarketData;

public class YahooSymbolsTests
{
    [Theory]
    [InlineData("AAPL.US", "AAPL")]
    [InlineData("TSLA.US", "TSLA")]
    public void The_market_suffix_of_the_united_states_is_dropped(string broker, string yahoo) =>
        Assert.Equal(yahoo, YahooSymbols.ToYahoo(broker));

    [Theory]
    [InlineData("SAN.ES", "SAN.ES")]
    [InlineData("IBE.ES", "IBE.ES")]
    [InlineData("ASML.NL", "ASML.NL")]
    public void Any_other_market_keeps_its_suffix(string broker, string yahoo) =>
        Assert.Equal(yahoo, YahooSymbols.ToYahoo(broker));

    [Fact]
    public void An_exception_wins_over_the_general_rule()
    {
        // Una regla más lista devolvería el precio de otro valor, y eso no falla: miente.
        Assert.Equal("CSPX.L", YahooSymbols.ToYahoo("CSPX.UK"));
    }
}

public class YahooMarketPriceProviderTests
{
    [Fact]
    public async Task Several_symbols_are_asked_for_in_a_single_call()
    {
        // Una llamada por valor agotaría el límite de peticiones en cuanto la cartera
        // tenga unas cuantas posiciones.
        var handler = new CapturingHandler("""
            {"quoteResponse":{"result":[
              {"symbol":"SAN.ES","currency":"EUR","regularMarketPrice":4.55,"regularMarketTime":1730000000},
              {"symbol":"IBE.ES","currency":"EUR","regularMarketPrice":12.10,"regularMarketTime":1730000000}
            ]}}
            """);

        var prices = await Provider(handler).GetPricesAsync(["SAN.ES", "IBE.ES"]);

        Assert.Single(handler.Requests);
        Assert.Equal(4.55m, prices["SAN.ES"].PriceInEuros);
        Assert.Equal(12.10m, prices["IBE.ES"].PriceInEuros);
    }

    [Fact]
    public async Task A_symbol_the_provider_does_not_know_does_not_take_the_rest_with_it()
    {
        var handler = new CapturingHandler("""
            {"quoteResponse":{"result":[
              {"symbol":"SAN.ES","currency":"EUR","regularMarketPrice":4.55,"regularMarketTime":1730000000}
            ]}}
            """);

        var prices = await Provider(handler).GetPricesAsync(["SAN.ES", "INVENTADO.XX"]);

        Assert.True(prices.ContainsKey("SAN.ES"));
        Assert.False(prices.ContainsKey("INVENTADO.XX"));
    }

    [Fact]
    public async Task A_value_quoted_in_another_currency_is_left_without_price()
    {
        // Convertirlo a euros sin decirlo produciría una cifra que nadie puede comprobar.
        var handler = new CapturingHandler("""
            {"quoteResponse":{"result":[
              {"symbol":"AAPL","currency":"USD","regularMarketPrice":230.5,"regularMarketTime":1730000000}
            ]}}
            """);

        Assert.Empty(await Provider(handler).GetPricesAsync(["AAPL.US"]));
    }

    [Fact]
    public async Task A_provider_that_fails_leaves_the_portfolio_without_prices_and_not_without_answer()
    {
        var handler = new CapturingHandler(string.Empty, HttpStatusCode.ServiceUnavailable);

        Assert.Empty(await Provider(handler).GetPricesAsync(["SAN.ES"]));
    }

    private static YahooMarketPriceProvider Provider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo/") },
            new FakeTimeProvider(),
            NullLogger<YahooMarketPriceProvider>.Instance);

    internal sealed class CapturingHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        internal List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}

public class MarketPriceDispatcherTests
{
    [Fact]
    public async Task Each_asset_class_is_asked_of_its_own_provider()
    {
        var crypto = new RecordingProvider(("BTC", 60000m));
        var equity = new RecordingProvider(("SAN.ES", 4.55m));

        var prices = await Dispatcher(crypto, equity).GetPricesAsync(["BTC", "SAN.ES"]);

        Assert.Equal(["BTC"], crypto.Asked);
        Assert.Equal(["SAN.ES"], equity.Asked);
        Assert.Equal(2, prices.Count);
    }

    [Fact]
    public async Task A_class_without_a_provider_does_not_break_the_query()
    {
        var crypto = new RecordingProvider(("BTC", 60000m));

        var prices = await Dispatcher(crypto, equity: null).GetPricesAsync(["BTC", "SAN.ES"]);

        Assert.True(prices.ContainsKey("BTC"));
        Assert.False(prices.ContainsKey("SAN.ES"));
    }

    [Fact]
    public async Task Two_queries_in_a_row_cost_a_single_call()
    {
        // Los proveedores gratuitos limitan por minuto, y agotarlos deja sin precio a
        // quien esté mirando.
        var crypto = new RecordingProvider(("BTC", 60000m));
        var dispatcher = Dispatcher(crypto, equity: null);

        await dispatcher.GetPricesAsync(["BTC"]);
        await dispatcher.GetPricesAsync(["BTC"]);

        Assert.Single(crypto.Calls);
    }

    private static MarketPriceDispatcher Dispatcher(RecordingProvider crypto, RecordingProvider? equity)
    {
        List<ClassifiedPriceProvider> providers = [new(AssetClass.Crypto, crypto)];

        if (equity is not null)
        {
            providers.Add(new ClassifiedPriceProvider(AssetClass.Equity, equity));
        }

        return new MarketPriceDispatcher(
            providers,
            new FixedClasses(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MarketPriceDispatcher>.Instance);
    }

    private sealed class FixedClasses : IAssetClassLookup
    {
        public Task<IReadOnlyDictionary<string, AssetClass>> ClassifyAsync(
            IReadOnlyCollection<string> canonicalSymbols,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AssetClass>>(
                canonicalSymbols.ToDictionary(
                    symbol => symbol,
                    symbol => symbol.Contains('.', StringComparison.Ordinal) ? AssetClass.Equity : AssetClass.Crypto,
                    StringComparer.OrdinalIgnoreCase));
    }

    internal sealed class RecordingProvider(params (string Symbol, decimal Price)[] known) : IMarketPriceProvider
    {
        internal List<string> Asked { get; } = [];

        internal List<int> Calls { get; } = [];

        public Task<IReadOnlyDictionary<string, MarketPrice>> GetPricesAsync(
            IReadOnlyCollection<string> canonicalSymbols,
            CancellationToken cancellationToken = default)
        {
            Asked.AddRange(canonicalSymbols);
            Calls.Add(canonicalSymbols.Count);

            var prices = known
                .Where(entry => canonicalSymbols.Contains(entry.Symbol, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    entry => entry.Symbol,
                    entry => new MarketPrice(entry.Symbol, entry.Price, DateTimeOffset.UnixEpoch),
                    StringComparer.OrdinalIgnoreCase);

            return Task.FromResult<IReadOnlyDictionary<string, MarketPrice>>(prices);
        }
    }
}

public class CoinGeckoCoverageTests
{
    /// <summary>
    /// Los activos de cripto que el usuario tiene tras importar Bit2Me y Kraken.
    /// </summary>
    /// <remarks>
    /// Un símbolo que falte en la tabla no rompe nada: deja la posición sin valorar y el
    /// patrimonio sale corto sin que se note. Por eso la lista está escrita aquí, y no
    /// deducida de la propia tabla, que se aprobaría a sí misma.
    /// </remarks>
    private static readonly string[] Portfolio =
        ["BTC", "ETH", "XRP", "SOL", "ADA", "DOGE", "B2M", "TAO", "PAXG", "PEPE", "XDC", "POL", "EURC", "USDG"];

    [Fact]
    public void Every_crypto_in_the_portfolio_has_an_identifier()
    {
        var missing = Portfolio.Where(symbol => !CoinGeckoMarketPriceProvider.CoinIds.ContainsKey(symbol)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void The_codes_of_kraken_reach_the_table_already_canonical()
    {
        // Kraken llama XXDG a Dogecoin y XXBT a Bitcoin; sin traducir, ningún precio.
        Assert.True(CoinGeckoMarketPriceProvider.CoinIds.ContainsKey(KrakenSymbols.ToCanonical("XXDG")));
        Assert.True(CoinGeckoMarketPriceProvider.CoinIds.ContainsKey(KrakenSymbols.ToCanonical("XXBT")));
    }

    [Fact]
    public void No_identifier_is_shared_by_two_symbols()
    {
        // Dos símbolos con el mismo identificador significa que uno está mal copiado, y
        // la cartera mostraría el precio de otra moneda.
        var repeated = CoinGeckoMarketPriceProvider.CoinIds
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(repeated);
    }
}

public class MarketPriceCacheExpiryTests
{
    [Fact]
    public async Task Once_the_window_is_over_the_price_is_asked_for_again()
    {
        var clock = new FakeTimeProvider();
        var cache = new MemoryCache(new MemoryCacheOptions { Clock = new ClockAdapter(clock) });
        var crypto = new MarketPriceDispatcherTests.RecordingProvider(("BTC", 60000m));
        var dispatcher = new MarketPriceDispatcher(
            [new ClassifiedPriceProvider(AssetClass.Crypto, crypto)],
            new AllCrypto(),
            cache,
            NullLogger<MarketPriceDispatcher>.Instance);

        await dispatcher.GetPricesAsync(["BTC"]);
        clock.Advance(MarketPriceDispatcher.Freshness + TimeSpan.FromSeconds(1));
        await dispatcher.GetPricesAsync(["BTC"]);

        Assert.Equal(2, crypto.Calls.Count);
    }

    /// <summary>El reloj de la caché, para no esperar un minuto real en la prueba.</summary>
    private sealed class ClockAdapter(TimeProvider time) : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }

    private sealed class AllCrypto : IAssetClassLookup
    {
        public Task<IReadOnlyDictionary<string, AssetClass>> ClassifyAsync(
            IReadOnlyCollection<string> canonicalSymbols,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, AssetClass>>(
                canonicalSymbols.ToDictionary(symbol => symbol, _ => AssetClass.Crypto, StringComparer.OrdinalIgnoreCase));
    }

}
