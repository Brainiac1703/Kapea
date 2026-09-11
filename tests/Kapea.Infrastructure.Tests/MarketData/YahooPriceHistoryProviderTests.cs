using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Infrastructure.MarketData;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.MarketData;

public class YahooSymbolHistoryTests
{
    [Fact]
    public void A_crypto_is_asked_for_against_the_euro()
    {
        // Yahoo cotiza las criptomonedas contra una divisa y lo dice en el símbolo.
        // Pedir «BTC» a secas devuelve otra cosa o nada.
        Assert.Equal("BTC-EUR", YahooSymbols.ToYahoo("BTC", AssetClass.Crypto));
    }

    [Fact]
    public void An_equity_keeps_the_rule_of_its_market() =>
        Assert.Equal("SAN.ES", YahooSymbols.ToYahoo("SAN.ES", AssetClass.Equity));

    [Fact]
    public void An_equity_of_the_united_states_drops_its_suffix() =>
        Assert.Equal("AAPL", YahooSymbols.ToYahoo("AAPL.US", AssetClass.Equity));
}

public class YahooPriceHistoryProviderTests
{
    private static readonly Guid Asset = Guid.NewGuid();

    [Fact]
    public async Task The_daily_closes_of_the_range_come_back_in_euros()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-history-btc.json"));

        var prices = await Provider(handler).GetHistoryAsync(Request());

        Assert.NotEmpty(prices);
        Assert.All(prices, price => Assert.Equal(Asset, price.AssetId));
        Assert.All(prices, price => Assert.Equal("Yahoo", price.Source));
        Assert.Equal(85401.4140625m, prices[0].PriceInEuros);
        Assert.Equal(new DateOnly(2025, 5, 1), prices[0].Date);
    }

    [Fact]
    public async Task A_day_without_a_close_is_left_out_instead_of_repeated()
    {
        // La respuesta grabada trae un día en nulo. Rellenarlo con el anterior dibujaría
        // una línea plana que nadie cotizó.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-history-btc.json"));

        var prices = await Provider(handler).GetHistoryAsync(Request());

        Assert.DoesNotContain(prices, price => price.Date == new DateOnly(2025, 5, 3));
    }

    [Fact]
    public async Task A_symbol_yahoo_does_not_cover_is_not_a_failure()
    {
        // Responde 404. No es que el sistema falle: es que esta fuente no lo cubre, y
        // los demás activos tienen que poder descargarse igual.
        var handler = new RecordedResponseHandler().Respond(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
        {
            Content = new StringContent(File(Recorded("yahoo-history-unknown.json"))),
        });

        Assert.Empty(await Provider(handler).GetHistoryAsync(Request("B2M")));
    }

    [Fact]
    public async Task A_provider_that_fails_leaves_the_series_empty_and_not_the_process_broken()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(System.Net.HttpStatusCode.ServiceUnavailable);

        Assert.Empty(await Provider(handler).GetHistoryAsync(Request()));
    }

    [Fact]
    public async Task The_range_asked_for_covers_both_ends()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-history-btc.json"));

        await Provider(handler).GetHistoryAsync(Request());

        var query = Assert.Single(handler.Requests).RequestUri!.Query;

        // Desde el principio del primer día hasta el final del último: Yahoo excluye el
        // extremo superior, y sin sumar el día se perdería siempre el más reciente.
        Assert.Contains("period1=1746057600", query, StringComparison.Ordinal);
        Assert.Contains("period2=1746576000", query, StringComparison.Ordinal);
    }

    private static string File(string path) =>
        System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, path));

    private static string Recorded(string fileName) => Path.Combine("MarketData", "Recorded", fileName);

    private static PriceHistoryRequest Request(string symbol = "BTC") =>
        new(Asset, symbol, AssetClass.Crypto, new DateOnly(2025, 5, 1), new DateOnly(2025, 5, 6));

    private static YahooPriceHistoryProvider Provider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo/") },
            NullLogger<YahooPriceHistoryProvider>.Instance);
}
