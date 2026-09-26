using System.Net;
using Kapea.Domain.Assets;
using Kapea.Infrastructure.MarketData;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.MarketData;

public class AssetSearchProviderTests
{
    [Fact]
    public async Task CoinGecko_brings_the_identifier_with_which_it_knows_each_coin()
    {
        // Es lo que convierte la búsqueda en algo más que comodidad: con el
        // identificador, una moneda fuera de la lista escrita a mano puede tener precios.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("coingecko-search.json"));

        var results = await CoinGecko(handler).SearchAsync("cardano");

        var cardano = results[0];

        Assert.Equal("ADA", cardano.Symbol);
        Assert.Equal("Cardano", cardano.Name);
        Assert.Equal("cardano", cardano.ProviderId);
        Assert.Equal(AssetClass.Crypto, cardano.Class);

        // Dos monedas comparten el símbolo ADA: se enseñan las dos y elige el usuario.
        Assert.Equal(2, results.Count);
        Assert.Equal("ada-the-ai-dog", results[1].ProviderId);
    }

    [Fact]
    public async Task Yahoo_only_brings_what_can_actually_be_held()
    {
        // Devuelve también índices y divisas. Ofrecer un índice para seguirlo llevaría a
        // una posición que nunca se puede tener.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("yahoo-search.json"));

        var results = await Yahoo(handler).SearchAsync("servicenow");

        Assert.DoesNotContain(results, result => result.Symbol == "^GSPC");
        Assert.Equal(["NOW", "NOWG.L"], results.Select(result => result.Symbol));

        var serviceNow = results[0];

        Assert.Equal("ServiceNow, Inc.", serviceNow.Name);
        Assert.Equal("NYSE", serviceNow.Market);
        Assert.Equal(AssetClass.Equity, serviceNow.Class);
    }

    [Fact]
    public async Task A_provider_that_fails_says_so_instead_of_pretending_there_is_nothing()
    {
        // Devolver vacío se leería como «no existe», y lo que pasa es que no se sabe.
        var handler = new RecordedResponseHandler().RespondWithStatus(HttpStatusCode.TooManyRequests);

        await Assert.ThrowsAsync<AssetSearchUnavailableException>(() => CoinGecko(handler).SearchAsync("bitcoin"));
    }

    [Fact]
    public async Task An_empty_search_asks_nobody()
    {
        var handler = new RecordedResponseHandler();

        Assert.Empty(await CoinGecko(handler).SearchAsync("   "));
        Assert.Empty(handler.Requests);
    }

    private static string Recorded(string fileName) => Path.Combine("MarketData", "Recorded", fileName);

    private static CoinGeckoAssetSearchProvider CoinGecko(RecordedResponseHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.coingecko.com/") },
            NullLogger<CoinGeckoAssetSearchProvider>.Instance);

    private static YahooAssetSearchProvider Yahoo(RecordedResponseHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") },
            NullLogger<YahooAssetSearchProvider>.Instance);
}
