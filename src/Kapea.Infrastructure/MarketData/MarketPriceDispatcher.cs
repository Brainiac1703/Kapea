using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>Sabe a qué clase de activo pertenece cada símbolo.</summary>
public interface IAssetClassLookup
{
    Task<IReadOnlyDictionary<string, AssetClass>> ClassifyAsync(
        IReadOnlyCollection<string> canonicalSymbols,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reparte los símbolos entre los proveedores según su clase y junta las respuestas.
/// </summary>
/// <remarks>
/// Una acción y una criptomoneda no se cotizan en el mismo sitio, y preguntar a los dos
/// por todo gastaría el doble de peticiones para tirar la mitad. Una clase sin proveedor
/// no rompe nada: sus posiciones se quedan sin valor de mercado, que es como estaban.
///
/// Los precios se guardan un rato porque abrir la cartera dos veces seguidas no debería
/// costar dos llamadas: los proveedores gratuitos limitan por minuto y agotarlos deja
/// sin precio a quien esté mirando.
/// </remarks>
public sealed class MarketPriceDispatcher(
    IEnumerable<ClassifiedPriceProvider> providers,
    IAssetClassLookup classes,
    IMemoryCache cache,
    ILogger<MarketPriceDispatcher> logger) : IMarketPriceProvider
{
    /// <summary>
    /// Cuánto vale un precio guardado.
    /// </summary>
    /// <remarks>
    /// Un minuto: bastante para que abrir la pantalla y refrescarla no cueste dos
    /// llamadas, y poco para que el precio siga siendo el de ahora. Una cartera no se
    /// mira con la frecuencia de un operador.
    /// </remarks>
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(1);

    public async Task<IReadOnlyDictionary<string, MarketPrice>> GetPricesAsync(
        IReadOnlyCollection<QuotedAsset> assets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var prices = new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<QuotedAsset>();

        // La caché sigue yendo por símbolo: el catálogo tiene índice único por símbolo y
        // clase, así que dos activos de la misma clase no pueden compartirlo.
        foreach (var asset in assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.CanonicalSymbol))
            .DistinctBy(asset => asset.CanonicalSymbol, StringComparer.OrdinalIgnoreCase))
        {
            if (cache.TryGetValue(Key(asset.CanonicalSymbol), out MarketPrice? cached) && cached is not null)
            {
                prices[asset.CanonicalSymbol] = cached;
            }
            else
            {
                pending.Add(asset);
            }
        }

        if (pending.Count == 0)
        {
            return prices;
        }

        var byClass = await classes
            .ClassifyAsync([.. pending.Select(asset => asset.CanonicalSymbol)], cancellationToken)
            .ConfigureAwait(false);

        foreach (var provider in providers)
        {
            var mine = pending
                .Where(asset => byClass.TryGetValue(asset.CanonicalSymbol, out var assetClass)
                    && assetClass == provider.AssetClass)
                .ToList();

            if (mine.Count == 0)
            {
                continue;
            }

            var fetched = await provider.Provider.GetPricesAsync(mine, cancellationToken).ConfigureAwait(false);

            foreach (var (symbol, price) in fetched)
            {
                prices[symbol] = price;
                cache.Set(Key(symbol), price, Freshness);
            }

            if (fetched.Count < mine.Count)
            {
                logger.LogInformation(
                    "Sin precio para {Cuantos} de {Total} activos de {Clase}.",
                    mine.Count - fetched.Count, mine.Count, provider.AssetClass);
            }
        }

        return prices;
    }

    private static string Key(string symbol) => $"precio:{symbol.ToUpperInvariant()}";
}

/// <summary>Un proveedor y la clase de activo que cubre.</summary>
public sealed record ClassifiedPriceProvider(AssetClass AssetClass, IMarketPriceProvider Provider);
