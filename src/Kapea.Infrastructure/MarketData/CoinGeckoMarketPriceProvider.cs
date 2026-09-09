using System.Globalization;
using System.Text.Json;
using Kapea.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Precios de criptomonedas desde CoinGecko. Su capa gratuita no exige clave y basta
/// para valorar una cartera personal.
/// </summary>
/// <remarks>
/// Solo cubre cripto: la renta variable se queda sin precio en esta fase y sus
/// posiciones lo indican. El proveedor definitivo para renta variable se decide
/// cuando el panel lo exija de verdad.
///
/// CoinGecko no acepta símbolos como BTC, sino sus identificadores propios, y un
/// mismo símbolo puede corresponder a varias monedas. Se usa una tabla explícita en
/// vez de resolver por símbolo para no acabar valorando la cartera con el precio de
/// un token homónimo.
/// </remarks>
public sealed class CoinGeckoMarketPriceProvider(
    HttpClient httpClient,
    TimeProvider timeProvider,
    ILogger<CoinGeckoMarketPriceProvider> logger) : IMarketPriceProvider
{
    internal static readonly IReadOnlyDictionary<string, string> CoinIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BTC"] = "bitcoin",
            ["ETH"] = "ethereum",
            ["XRP"] = "ripple",
            ["LTC"] = "litecoin",
            ["ADA"] = "cardano",
            ["SOL"] = "solana",
            ["DOT"] = "polkadot",
            ["MATIC"] = "matic-network",
            ["LINK"] = "chainlink",
            ["XLM"] = "stellar",
            ["DOGE"] = "dogecoin",
            ["AVAX"] = "avalanche-2",
            ["ATOM"] = "cosmos",
            ["ALGO"] = "algorand",
            ["XMR"] = "monero",
            ["BCH"] = "bitcoin-cash",
            ["UNI"] = "uniswap",
            ["ETC"] = "ethereum-classic",
        };

    public async Task<IReadOnlyDictionary<string, MarketPrice>> GetPricesAsync(
        IReadOnlyCollection<string> canonicalSymbols,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalSymbols);

        var wanted = canonicalSymbols
            .Where(symbol => CoinIds.ContainsKey(symbol))
            .ToDictionary(symbol => CoinIds[symbol], symbol => symbol, StringComparer.Ordinal);

        if (wanted.Count == 0)
        {
            return new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
        }

        var query = $"api/v3/simple/price?ids={Uri.EscapeDataString(string.Join(',', wanted.Keys))}" +
            "&vs_currencies=eur&include_last_updated_at=true";

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
            // Sin precio, la cartera sigue mostrando cantidad y coste medio. Degradar es
            // preferible a dejar sin respuesta una consulta que no depende del precio.
            logger.LogWarning(exception, "CoinGecko no ha respondido; las posiciones se mostrarán sin valor de mercado.");

            return new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Dictionary<string, MarketPrice> Read(JsonDocument document, Dictionary<string, string> wanted)
    {
        var prices = new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);

        foreach (var coin in document.RootElement.EnumerateObject())
        {
            if (!wanted.TryGetValue(coin.Name, out var symbol)
                || !coin.Value.TryGetProperty("eur", out var eur)
                || !TryReadDecimal(eur, out var price)
                || price <= 0m)
            {
                continue;
            }

            var asOf = coin.Value.TryGetProperty("last_updated_at", out var updated) && updated.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : timeProvider.GetUtcNow();

            prices[symbol] = new MarketPrice(symbol, price, asOf);
        }

        return prices;
    }

    private static bool TryReadDecimal(JsonElement element, out decimal value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);

            case JsonValueKind.String:
                return decimal.TryParse(
                    element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

            default:
                value = 0m;

                return false;
        }
    }
}
