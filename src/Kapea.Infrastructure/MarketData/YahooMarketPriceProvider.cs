using System.Globalization;
using System.Text.Json;
using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Precios de renta variable desde Yahoo Finance.
/// </summary>
/// <remarks>
/// No es una API con contrato publicado, así que puede cambiar sin aviso. Se elige
/// igualmente porque no exige clave ni cuota para una cartera personal, y porque está
/// detrás de un puerto: cambiar de proveedor es escribir otra implementación, no tocar
/// la cartera. Si deja de responder, las posiciones se muestran sin valor de mercado.
///
/// La cotización se pide por el mismo endpoint que la serie histórica y no por el de
/// cotizaciones, que exige una cookie y un identificador de sesión que Yahoo no
/// documenta: sin ellos responde 401 siempre, no de vez en cuando. La cabecera de la
/// serie trae el precio del momento y su instante, así que no se pierde nada por el
/// camino; lo que cuesta es una petición por valor en lugar de una para todos.
/// </remarks>
public sealed class YahooMarketPriceProvider(
    HttpClient httpClient,
    IExchangeRateProvider exchangeRates,
    TimeProvider timeProvider,
    ILogger<YahooMarketPriceProvider> logger) : IMarketPriceProvider
{
    /// <summary>Deja el cliente listo para hablar con Yahoo.</summary>
    /// <remarks>Yahoo rechaza las peticiones sin agente de usuario reconocible.</remarks>
    public static void Configure(HttpClient client, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.BaseAddress = baseAddress;
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; Kapea/1.0)");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<IReadOnlyDictionary<string, MarketPrice>> GetPricesAsync(
        IReadOnlyCollection<QuotedAsset> assets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var prices = new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in assets
            .Select(asset => asset.CanonicalSymbol)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // De uno en uno, y por eso cada uno con su intento: con una sola llamada un
            // fallo dejaba sin precio la clase entera, y ahora no tiene por qué.
            var price = await PriceAsync(symbol, cancellationToken).ConfigureAwait(false);

            if (price is not null)
            {
                prices[symbol] = price;
            }
        }

        return prices;
    }

    private async Task<MarketPrice?> PriceAsync(string symbol, CancellationToken cancellationToken)
    {
        // Del símbolo del bróker al de Yahoo. Un solo día: lo que interesa es la cabecera
        // de la respuesta, no la serie, y pedir menos es gastar menos cuota.
        var yahooSymbol = YahooSymbols.ToYahoo(symbol);
        var query = $"v8/finance/chart/{Uri.EscapeDataString(yahooSymbol)}?range=1d&interval=1d";

        try
        {
            using var response = await httpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);

            // Un símbolo que Yahoo no conoce responde 404. No es un fallo del sistema:
            // es un valor que esta fuente no cubre.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);

            return await ReadAsync(document, symbol, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(
                exception, "Yahoo no ha dado el precio de {Simbolo}; se mostrará sin valor de mercado.", symbol);

            return null;
        }
    }

    private async Task<MarketPrice?> ReadAsync(
        JsonDocument document,
        string symbol,
        CancellationToken cancellationToken)
    {
        if (!document.RootElement.TryGetProperty("chart", out var chart)
            || !chart.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0
            || !results[0].TryGetProperty("meta", out var meta))
        {
            return null;
        }

        if (!TryPrice(meta, out var price))
        {
            return null;
        }

        var asOf = meta.TryGetProperty("regularMarketTime", out var time) && time.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : timeProvider.GetUtcNow();

        var currency = meta.TryGetProperty("currency", out var currencyNode)
            ? currencyNode.GetString()
            : null;

        if (string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            return new MarketPrice(symbol, price, asOf);
        }

        if (currency is not { Length: > 0 })
        {
            logger.LogInformation("Yahoo no dice en qué divisa cotiza {Simbolo}.", symbol);

            return null;
        }

        return await ToEurosAsync(symbol, price, currency, asOf, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pasa a euros un precio cotizado en otra divisa, al tipo del BCE.
    /// </summary>
    /// <remarks>
    /// Buena parte de la renta variable de una cartera española cotiza en dólares.
    /// Descartarla la dejaba sin valor de mercado sin que nada lo explicara, que es peor
    /// que convertirla: el tipo es el mismo con el que se valoran los movimientos, está
    /// guardado y se puede contrastar.
    ///
    /// Sin tipo no se entrega nada. Devolver la cifra en dólares como si fuera en euros
    /// daría un valor de cartera equivocado que nadie podría detectar.
    /// </remarks>
    private async Task<MarketPrice?> ToEurosAsync(
        string symbol,
        decimal price,
        string currency,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var money = Currency.FromCode(currency);
        var rate = await exchangeRates
            .ResolveAsync(money, DateOnly.FromDateTime(asOf.UtcDateTime), cancellationToken)
            .ConfigureAwait(false);

        if (rate is null)
        {
            logger.LogInformation(
                "Sin tipo de cambio de {Divisa}: {Simbolo} se queda sin valor de mercado.", currency, symbol);

            return null;
        }

        return new MarketPrice(symbol, rate.ToEuros(new Money(price, money)).Amount, asOf);
    }

    private static bool TryPrice(JsonElement meta, out decimal price)
    {
        price = 0m;

        if (!meta.TryGetProperty("regularMarketPrice", out var node))
        {
            return false;
        }

        return node.ValueKind switch
        {
            JsonValueKind.Number => node.TryGetDecimal(out price),
            JsonValueKind.String => decimal.TryParse(
                node.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out price),
            _ => false,
        };
    }
}
