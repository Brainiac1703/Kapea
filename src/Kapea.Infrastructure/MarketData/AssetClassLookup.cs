using Kapea.Domain.Assets;
using Kapea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// La clase de cada activo, tal y como la guarda el catálogo.
/// </summary>
/// <remarks>
/// Se lee del catálogo y no se deduce del símbolo: hay tickers de acciones que
/// coinciden con símbolos de criptomonedas, y adivinarlo pondría el precio de otra cosa.
/// </remarks>
public sealed class AssetClassLookup(KapeaDbContext context) : IAssetClassLookup
{
    public async Task<IReadOnlyDictionary<string, AssetClass>> ClassifyAsync(
        IReadOnlyCollection<string> canonicalSymbols,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonicalSymbols);

        var wanted = canonicalSymbols.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assets = await context.Assets
            .Select(asset => new { asset.CanonicalSymbol, asset.Class })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var classified = new Dictionary<string, AssetClass>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets.Where(asset => wanted.Contains(asset.CanonicalSymbol)))
        {
            classified[asset.CanonicalSymbol] = asset.Class;
        }

        return classified;
    }
}
