using Kapea.Application.Import;
using Kapea.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// Resuelve símbolos contra el catálogo. Un símbolo desconocido crea el activo como
/// no verificado: bloquear la importación entera por uno solo dejaría fuera todo lo
/// demás, que sí se entiende.
/// </summary>
public sealed class AssetCatalog(KapeaDbContext context, ILogger<AssetCatalog> logger) : IAssetCatalog
{
    public async Task<Asset> ResolveAsync(
        string symbol,
        AssetClass assetClass,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var canonical = symbol.Trim().ToUpperInvariant();

        var existing = await context.Assets
            .SingleOrDefaultAsync(
                asset => asset.CanonicalSymbol == canonical && asset.Class == assetClass,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            await WatchAsync(existing.Id, cancellationToken).ConfigureAwait(false);

            return existing;
        }

        // También se mira lo añadido en esta misma unidad de trabajo: dos registros del
        // mismo lote con el mismo activo no pueden crear dos entradas del catálogo.
        var pending = context.ChangeTracker.Entries<Asset>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(asset => asset.CanonicalSymbol == canonical && asset.Class == assetClass);

        if (pending is not null)
        {
            await WatchAsync(pending.Id, cancellationToken).ConfigureAwait(false);

            return pending;
        }

        var created = Asset.CreateUnverified(canonical, assetClass);
        await context.Assets.AddAsync(created, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Símbolo '{Simbolo}' desconocido: se crea como activo sin verificar, pendiente de revisión.", canonical);

        await WatchAsync(created.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <summary>
    /// Deja el activo en la lista de lo que el usuario vigila.
    /// </summary>
    /// <remarks>
    /// Tener posición ya cuenta como seguirlo, así que esto no hace falta para verlo
    /// mientras se tenga. Se añade para lo de después: al vender entero, el activo sigue
    /// en la lista, que es donde el usuario quiere encontrarlo para decidir si vuelve.
    /// </remarks>
    private async Task WatchAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var alreadyWatched = await context.WatchedAssets
            .AnyAsync(watched => watched.AssetId == assetId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyWatched
            || context.ChangeTracker.Entries<WatchedAsset>().Any(entry => entry.Entity.AssetId == assetId))
        {
            return;
        }

        await context.WatchedAssets
            .AddAsync(WatchedAsset.Of(context.CurrentUserId, assetId, DateTimeOffset.UtcNow), cancellationToken)
            .ConfigureAwait(false);
    }
}
