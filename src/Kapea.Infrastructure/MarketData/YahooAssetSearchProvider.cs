using System.Text.Json;
using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Busca renta variable en Yahoo Finance por nombre o por símbolo.
/// </summary>
/// <remarks>
/// Yahoo devuelve de todo: acciones, fondos, índices, divisas y futuros. Sólo se
/// quedan los instrumentos que Kapea sabe tratar como posición —acciones y fondos
/// cotizados—, porque ofrecer un índice para seguirlo llevaría a una posición que nunca
/// se puede tener.
/// </remarks>
public sealed class YahooAssetSearchProvider(
    HttpClient httpClient,
    ILogger<YahooAssetSearchProvider> logger) : IAssetSearchProvider
{
    private const int Limit = 10;

    /// <summary>Lo que se puede comprar y tener. El resto se descarta.</summary>
    private static readonly string[] Tradable = ["EQUITY", "ETF", "MUTUALFUND"];

    public string Name => "Yahoo Finance";

    public async Task<IReadOnlyList<AssetSearchResult>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var query = $"v1/finance/search?q={Uri.EscapeDataString(text.Trim())}" +
            $"&quotesCount={Limit}&newsCount=0&listsCount=0";

        try
        {
            using var response = await httpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument
                .ParseAsync(content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return Read(document);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Yahoo no ha respondido a la búsqueda de '{Texto}'.", text);

            throw new AssetSearchUnavailableException(Name, exception);
        }
    }

    private List<AssetSearchResult> Read(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("quotes", out var quotes)
            || quotes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<AssetSearchResult>();

        foreach (var quote in quotes.EnumerateArray())
        {
            var type = quote.TryGetProperty("quoteType", out var kind) ? kind.GetString() : null;

            if (type is null || !Tradable.Contains(type, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var symbol = quote.TryGetProperty("symbol", out var ticker) ? ticker.GetString() : null;

            if (string.IsNullOrWhiteSpace(symbol))
            {
                continue;
            }

            var name = Text(quote, "longname") ?? Text(quote, "shortname") ?? symbol;

            results.Add(new AssetSearchResult(
                symbol.Trim().ToUpperInvariant(),
                name.Trim(),
                AssetClass.Equity,

                // En Yahoo el identificador es el propio símbolo: no hay otro.
                symbol.Trim().ToUpperInvariant(),
                Name,
                Text(quote, "exchDisp") ?? Text(quote, "exchange")));
        }

        return results;
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
