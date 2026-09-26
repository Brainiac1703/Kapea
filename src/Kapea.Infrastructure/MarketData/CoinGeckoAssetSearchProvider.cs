using System.Text.Json;
using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Busca criptomonedas en CoinGecko por nombre o por símbolo.
/// </summary>
/// <remarks>
/// Lo que importa de cada resultado es su identificador: con él, una moneda que no está
/// en la lista escrita a mano puede tener precios, y dos monedas que comparten símbolo
/// dejan de confundirse porque el usuario ha elegido cuál.
/// </remarks>
public sealed class CoinGeckoAssetSearchProvider(
    HttpClient httpClient,
    ILogger<CoinGeckoAssetSearchProvider> logger) : IAssetSearchProvider
{
    /// <summary>Cuántos resultados se enseñan. Más allá, la lista deja de ayudar a elegir.</summary>
    private const int Limit = 10;

    public string Name => "CoinGecko";

    public async Task<IReadOnlyList<AssetSearchResult>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            using var response = await httpClient
                .GetAsync($"api/v3/search?query={Uri.EscapeDataString(text.Trim())}", cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument
                .ParseAsync(content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return Read(document);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Un proveedor que no responde no puede vaciar la pantalla: quien llama
            // enseña lo del otro y dice que la búsqueda está incompleta.
            logger.LogWarning(exception, "CoinGecko no ha respondido a la búsqueda de '{Texto}'.", text);

            throw new AssetSearchUnavailableException(Name, exception);
        }
    }

    private List<AssetSearchResult> Read(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("coins", out var coins)
            || coins.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<AssetSearchResult>();

        foreach (var coin in coins.EnumerateArray().Take(Limit))
        {
            var id = coin.TryGetProperty("id", out var identifier) ? identifier.GetString() : null;
            var symbol = coin.TryGetProperty("symbol", out var ticker) ? ticker.GetString() : null;
            var name = coin.TryGetProperty("name", out var label) ? label.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(symbol))
            {
                continue;
            }

            results.Add(new AssetSearchResult(
                symbol.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(name) ? symbol.Trim().ToUpperInvariant() : name.Trim(),
                AssetClass.Crypto,
                id.Trim(),
                Name));
        }

        return results;
    }
}

/// <summary>Un proveedor no ha podido responder a la búsqueda.</summary>
public sealed class AssetSearchUnavailableException(string provider, Exception inner)
    : Exception($"El proveedor '{provider}' no ha respondido a la búsqueda.", inner)
{
    public string Provider { get; } = provider;
}
