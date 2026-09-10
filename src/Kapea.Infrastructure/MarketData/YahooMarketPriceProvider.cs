using System.Globalization;
using System.Text.Json;
using Kapea.Application.Abstractions;
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
/// </remarks>
public sealed class YahooMarketPriceProvider(
    HttpClient httpClient,
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
        IReadOnlyCollection<string> canonicalSymbols,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalSymbols);

        // Del símbolo de Yahoo al del bróker: la respuesta viene con el suyo y hay que
        // devolver el nuestro, que es el que conoce la cartera.
        var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in canonicalSymbols.Where(symbol => !string.IsNullOrWhiteSpace(symbol)))
        {
            wanted[YahooSymbols.ToYahoo(symbol)] = symbol;
        }

        if (wanted.Count == 0)
        {
            return new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
        }

        // Todos en una sola llamada: una por valor agotaría el límite de peticiones en
        // cuanto la cartera tenga unas cuantas posiciones.
        var query = "v7/finance/quote?symbols=" + Uri.EscapeDataString(string.Join(',', wanted.Keys));

        try
        {
            using var response = await httpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);

            return Read(document, wanted);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(
                exception, "Yahoo no ha respondido; las acciones se mostrarán sin valor de mercado.");

            return new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, MarketPrice> Read(JsonDocument document, Dictionary<string, string> wanted)
    {
        var prices = new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);

        if (!document.RootElement.TryGetProperty("quoteResponse", out var quoteResponse)
            || !quoteResponse.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            return prices;
        }

        foreach (var quote in results.EnumerateArray())
        {
            if (!quote.TryGetProperty("symbol", out var symbolNode)
                || symbolNode.GetString() is not { Length: > 0 } yahooSymbol
                || !wanted.TryGetValue(yahooSymbol, out var symbol))
            {
                continue;
            }

            var currency = quote.TryGetProperty("currency", out var currencyNode)
                ? currencyNode.GetString()
                : null;

            // Un valor cotizado en dólares no se convierte aquí: el precio se entrega en
            // la divisa en la que cotiza, y convertirlo a euros sin decirlo produciría
            // una cifra que nadie puede comprobar.
            if (!string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "El valor {Simbolo} cotiza en {Divisa} y todavía no se convierte a euros.", symbol, currency);

                continue;
            }

            if (!TryPrice(quote, out var price))
            {
                continue;
            }

            var asOf = quote.TryGetProperty("regularMarketTime", out var time) && time.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : timeProvider.GetUtcNow();

            prices[symbol] = new MarketPrice(symbol, price, asOf);
        }

        return prices;
    }

    private static bool TryPrice(JsonElement quote, out decimal price)
    {
        price = 0m;

        if (!quote.TryGetProperty("regularMarketPrice", out var node))
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
