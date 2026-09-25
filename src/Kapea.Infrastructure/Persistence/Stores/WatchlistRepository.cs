using Kapea.Application.Abstractions;
using Kapea.Application.Watchlist;
using Kapea.Domain.Assets;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// Lee y escribe qué activos vigila el usuario.
/// </summary>
/// <remarks>
/// La pertenencia a la lista se resuelve como una unión: está en seguimiento lo que el
/// usuario añadió y lo que tiene en cartera. Preguntarlo así evita tener que reconciliar
/// la lista con las posiciones cada vez que una cambia.
/// </remarks>
public sealed class WatchlistRepository(KapeaDbContext context, IMarketPriceProvider prices) : IWatchlistRepository
{
    public async Task<IReadOnlyList<WatchedAssetResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var held = await HeldQuantitiesAsync(cancellationToken).ConfigureAwait(false);

        var watchedIds = await context.WatchedAssets
            .Select(watched => watched.AssetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ids = watchedIds.Concat(held.Keys).ToHashSet();

        var assets = await context.Assets
            .Where(asset => ids.Contains(asset.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var quotes = await prices
            .GetPricesAsync([.. assets.Select(asset => asset.CanonicalSymbol)], cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. assets
                .Select(asset => Response(asset, held.GetValueOrDefault(asset.Id), quotes.GetValueOrDefault(asset.CanonicalSymbol)))
                .OrderByDescending(watched => watched.IsHeld)
                .ThenBy(watched => watched.Symbol, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public async Task<Asset?> FindAsync(
        string symbol,
        AssetClass assetClass,
        CancellationToken cancellationToken = default)
    {
        var canonical = symbol.Trim().ToUpperInvariant();

        return await context.Assets
            .SingleOrDefaultAsync(
                asset => asset.CanonicalSymbol == canonical && asset.Class == assetClass,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Asset> AddToCatalogueAsync(
        string symbol,
        AssetClass assetClass,
        CancellationToken cancellationToken = default,
        string? displayName = null,
        string? providerId = null)
    {
        // Lo añade el usuario a mano, así que es lo que él dice que es: entra verificado.
        // Lo que no se sabe todavía es si algún proveedor lo cubre, y eso se comprueba
        // aparte y se le dice.
        var asset = Asset.Create(symbol, assetClass, displayName, isin: null, providerId);

        await context.Assets.AddAsync(asset, cancellationToken).ConfigureAwait(false);

        return asset;
    }

    /// <summary>
    /// Busca en el catálogo el activo al que corresponde un resultado.
    /// </summary>
    /// <remarks>
    /// Primero por el identificador del proveedor, que es lo más fiable. Después
    /// traduciendo cada activo al símbolo del proveedor con la misma función que se usa
    /// para pedir precios: es lo que hace que elegir «NOW» encuentre el «NOW.US» que
    /// entró importando XTB, en lugar de partir su historial en dos.
    /// </remarks>
    public async Task<Asset?> FindByProviderAsync(
        AssetSearchResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var candidates = await context.Assets
            .Where(asset => asset.Class == result.Class)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var known = candidates.Find(asset => asset.ProviderId == result.ProviderId);

        if (known is not null)
        {
            return known;
        }

        return candidates.Find(asset =>
            asset.CanonicalSymbol.Equals(result.Symbol, StringComparison.OrdinalIgnoreCase)
            || (result.Class == AssetClass.Equity
                && MarketData.YahooSymbols.ToYahoo(asset.CanonicalSymbol)
                    .Equals(result.Symbol, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<bool> IsWatchedAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        context.WatchedAssets.AnyAsync(watched => watched.AssetId == assetId, cancellationToken);

    public async Task WatchAsync(Guid assetId, DateTimeOffset addedAt, CancellationToken cancellationToken = default) =>
        await context.WatchedAssets
            .AddAsync(WatchedAsset.Of(context.CurrentUserId, assetId, addedAt), cancellationToken)
            .ConfigureAwait(false);

    public async Task StopWatchingAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        var watched = await context.WatchedAssets
            .SingleOrDefaultAsync(entry => entry.AssetId == assetId, cancellationToken)
            .ConfigureAwait(false);

        if (watched is not null)
        {
            context.WatchedAssets.Remove(watched);
        }
    }

    public async Task<bool> IsHeldAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        (await HeldQuantitiesAsync(cancellationToken).ConfigureAwait(false)).ContainsKey(assetId);

    public async Task<WatchedAssetResponse?> FindWatchedAsync(Guid assetId, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(watched => watched.AssetId == assetId);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    /// <summary>Lo que queda vivo en lotes, por activo. Un activo sin lotes no aparece.</summary>
    private async Task<Dictionary<Guid, decimal>> HeldQuantitiesAsync(CancellationToken cancellationToken)
    {
        var lots = await context.Lots
            .Select(lot => new { lot.AssetId, lot.RemainingQuantity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return lots
            .GroupBy(lot => lot.AssetId)
            .ToDictionary(group => group.Key, group => group.Sum(lot => lot.RemainingQuantity.Value))
            .Where(entry => entry.Value > 0m)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static WatchedAssetResponse Response(Asset asset, decimal quantity, MarketPrice? quote) =>
        new(
            asset.Id,
            asset.CanonicalSymbol,
            asset.DisplayName,
            asset.Class.ToString(),
            quantity > 0m,
            quantity > 0m ? quantity : null,
            quantity > 0m && quote is not null ? quantity * quote.PriceInEuros : null,
            quote?.PriceInEuros,
            quote is null ? null : DateOnly.FromDateTime(quote.AsOf.UtcDateTime));
}
