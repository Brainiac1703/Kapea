using Kapea.Application.MarketData;
using Kapea.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// Los activos que hay que valorar y desde cuándo.
/// </summary>
/// <remarks>
/// Salta el filtro por usuario a propósito: los precios son un catálogo global, y
/// descargar dos veces lo que valió bitcoin el martes solo gastaría cuota. Aun así no
/// devuelve nada de nadie, solo qué activos hay que mirar.
/// </remarks>
public sealed class PricedAssetRepository(KapeaDbContext context) : IPricedAssetRepository
{
    public async Task<IReadOnlyList<PricedAsset>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Con lotes abiertos: un activo vendido del todo ya no necesita precio nuevo, y
        // su historia pasada sigue guardada para las gráficas de entonces.
        //
        // La cantidad pendiente es un objeto de valor y la base de datos no sabe
        // compararlo, así que el filtro se hace aquí sobre lo mínimo: activo y cantidad.
        var lots = await context.Lots
            .IgnoreQueryFilters()
            .Select(lot => new { lot.AssetId, lot.RemainingQuantity })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var held = lots
            .Where(lot => !lot.RemainingQuantity.IsZero)
            .Select(lot => lot.AssetId)
            .Distinct()
            .ToList();

        if (held.Count == 0)
        {
            return [];
        }

        var firstHeld = await context.Transactions
            .IgnoreQueryFilters()
            .Where(transaction => transaction.AssetId != null
                && held.Contains(transaction.AssetId.Value)
                && transaction.Type != TransactionType.Unknown)
            .GroupBy(transaction => transaction.AssetId!.Value)
            .Select(group => new { AssetId = group.Key, First = group.Min(t => t.OccurredAt.Instant) })
            .ToDictionaryAsync(entry => entry.AssetId, entry => entry.First, cancellationToken)
            .ConfigureAwait(false);

        var assets = await context.Assets
            .IgnoreQueryFilters()
            .Where(asset => held.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.CanonicalSymbol, asset.Class })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. assets
                .Where(asset => firstHeld.ContainsKey(asset.Id))
                .Select(asset => new PricedAsset(
                    asset.Id,
                    asset.CanonicalSymbol,
                    asset.Class,
                    DateOnly.FromDateTime(firstHeld[asset.Id].UtcDateTime)))
                .OrderBy(asset => asset.CanonicalSymbol, StringComparer.Ordinal),
        ];
    }
}
