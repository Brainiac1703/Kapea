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

        var prices = (await Provider(handler).GetHistoryAsync(Request())).Prices;

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

        var prices = (await Provider(handler).GetHistoryAsync(Request())).Prices;

        Assert.DoesNotContain(prices, price => price.Date == new DateOnly(2025, 5, 3));
    }

    [Fact]
    public async Task The_range_of_the_day_comes_back_with_the_close()
    {
        // Sin apertura, máximo y mínimo sólo se sabe dónde acabó el día: uno que subió
        // un ocho por ciento y volvió al punto de partida es indistinguible de uno plano.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-history-btc.json"));

        var prices = (await Provider(handler).GetHistoryAsync(Request())).Prices;

        Assert.True(prices[0].HasRange);
        Assert.Equal(83210.96875m, prices[0].OpenInEuros);
        Assert.Equal(86330.234375m, prices[0].HighInEuros);
        Assert.Equal(83158.65625m, prices[0].LowInEuros);
    }

    [Fact]
    public async Task The_range_is_converted_with_the_same_rate_as_the_close()
    {
        // Convertir sólo el cierre dejaría la vela con el cuerpo en euros y las mechas
        // en dólares.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-history-dollars.json"));

        var prices = (await Provider(handler, new FixedRate(1.10m)).GetHistoryAsync(Request("PAXG"))).Prices;

        Assert.Equal(100m, prices[0].PriceInEuros);
        Assert.True(prices[0].HasRange);
        Assert.Equal(105m, prices[0].HighInEuros);
        Assert.Equal(95m, prices[0].LowInEuros);
        Assert.Equal(98m, prices[0].OpenInEuros);
    }

    [Fact]
    public async Task A_symbol_yahoo_does_not_cover_is_not_a_failure()
    {
        // Responde 404. No es que el sistema falle: es que esta fuente no lo cubre, y
        // los demás activos tienen que poder descargarse igual.
        // Ni contra el euro ni contra el dólar: esta fuente no lo cubre, y los demás
        // activos tienen que poder descargarse igual.
        var handler = new RecordedResponseHandler()
            .Respond(_ => NotFound())
            .Respond(_ => NotFound());

        Assert.Empty((await Provider(handler).GetHistoryAsync(Request("B2M"))).Prices);
    }

    [Fact]
    public async Task A_provider_that_fails_leaves_the_series_empty_and_not_the_process_broken()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithStatus(System.Net.HttpStatusCode.ServiceUnavailable)
            .RespondWithStatus(System.Net.HttpStatusCode.ServiceUnavailable);

        Assert.Empty((await Provider(handler).GetHistoryAsync(Request())).Prices);
    }

    [Fact]
    public async Task A_provider_that_fails_says_it_could_not_answer()
    {
        // Es lo que separa un hueco de un día de uno para siempre: sin esto, el tramo
        // consta preguntado y no se vuelve a pedir.
        var handler = new RecordedResponseHandler()
            .RespondWithStatus(System.Net.HttpStatusCode.Unauthorized)
            .RespondWithStatus(System.Net.HttpStatusCode.Unauthorized);

        Assert.False((await Provider(handler).GetHistoryAsync(Request())).Answered);
    }

    [Fact]
    public async Task A_symbol_yahoo_does_not_know_is_an_answer_and_not_a_failure()
    {
        var handler = new RecordedResponseHandler()
            .Respond(_ => NotFound())
            .Respond(_ => NotFound());

        Assert.True((await Provider(handler).GetHistoryAsync(Request("B2M"))).Answered);
    }

    [Fact]
    public async Task A_failure_against_the_euro_does_not_spend_another_request_against_the_dollar()
    {
        // El precio de un token pequeño se busca contra el dólar si no lo hay contra el
        // euro. Insistir con el proveedor que acaba de rechazar la petición gasta cuota
        // y taparía el fallo con un segundo vacío.
        var handler = new RecordedResponseHandler()
            .RespondWithStatus(System.Net.HttpStatusCode.Unauthorized)
            .RespondWithFile(Recorded("yahoo-history-btc.json"));

        var result = await Provider(handler).GetHistoryAsync(Request());

        Assert.False(result.Answered);
        Assert.Single(handler.Requests);
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

    [Fact]
    public async Task A_crypto_without_a_euro_series_is_looked_up_against_the_dollar()
    {
        // Yahoo no cotiza contra el euro los tokens pequeños, y es la única fuente que
        // llega más atrás de un año. Rendirse tras la primera respuesta vacía dejaría un
        // hueco de meses en la gráfica.
        var handler = new RecordedResponseHandler()
            .Respond(_ => NotFound())
            .RespondWithFile(Recorded("yahoo-history-dollars.json"));

        var prices = (await Provider(handler, new FixedRate(1.10m)).GetHistoryAsync(Request("B2M"))).Prices;

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("B2M-EUR", handler.Requests[0].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("B2M-USD", handler.Requests[1].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.NotEmpty(prices);
    }

    [Fact]
    public async Task A_series_quoted_in_dollars_is_converted_with_the_rate_of_the_day()
    {
        // Yahoo no cotiza en euros los tokens pequeños, y es la única fuente que llega
        // más atrás de un año. Se convierte con el mismo tipo con el que se valoran los
        // movimientos, y el origen lo dice para no confundirlo con un precio cotizado.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-history-dollars.json"));

        var prices = (await Provider(handler, new FixedRate(1.10m)).GetHistoryAsync(Request("PAXG"))).Prices;


        Assert.Equal(100m, prices[0].PriceInEuros);
        Assert.Equal("Yahoo+BCE", prices[0].Source);
    }

    [Fact]
    public async Task A_day_without_an_exchange_rate_is_left_out_rather_than_guessed()
    {
        // Sin tipo no hay conversión posible, así que tampoco la hay probando en dólares.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Recorded("yahoo-history-dollars.json"))
            .RespondWithFile(Recorded("yahoo-history-dollars.json"));

        Assert.Empty((await Provider(handler).GetHistoryAsync(Request("PAXG"))).Prices);
    }

    private static HttpResponseMessage NotFound() =>
        new(System.Net.HttpStatusCode.NotFound)
        {
            Content = new StringContent(File(Recorded("yahoo-history-unknown.json"))),
        };

    private static string File(string path) =>
        System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, path));

    private static string Recorded(string fileName) => Path.Combine("MarketData", "Recorded", fileName);

    private static PriceHistoryRequest Request(string symbol = "BTC") =>
        new(Asset, symbol, AssetClass.Crypto, new DateOnly(2025, 5, 1), new DateOnly(2025, 5, 6));

    private static YahooPriceHistoryProvider Provider(
        HttpMessageHandler handler,
        IExchangeRateProvider? rates = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo/") },
            rates ?? new NoRates(),
            NullLogger<YahooPriceHistoryProvider>.Instance);

    /// <summary>Sin tipos de cambio guardados, que es el estado de partida.</summary>
    private sealed class NoRates : IExchangeRateProvider
    {
        public Task<Kapea.Domain.Exchange.ExchangeRate?> ResolveAsync(
            Kapea.Domain.ValueObjects.Currency currency,
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Kapea.Domain.Exchange.ExchangeRate?>(null);
    }

    /// <summary>Un tipo fijo, para leer la conversión a simple vista.</summary>
    private sealed class FixedRate(decimal unitsPerEuro) : IExchangeRateProvider
    {
        public Task<Kapea.Domain.Exchange.ExchangeRate?> ResolveAsync(
            Kapea.Domain.ValueObjects.Currency currency,
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Kapea.Domain.Exchange.ExchangeRate?>(Kapea.Domain.Exchange.ExchangeRate.Create(
                currency, unitsPerEuro, date, date, Kapea.Domain.Exchange.ExchangeRate.EuropeanCentralBank));
    }
}
