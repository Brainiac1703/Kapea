using Kapea.Application.Abstractions;
using Kapea.Domain.MarketData;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// La serie de precios sobre EF Core.
/// </summary>
/// <remarks>
/// Los precios son un catálogo global, así que se leen y se escriben sin filtro de
/// usuario: lo que valió un activo el martes es lo mismo para todos.
/// </remarks>
public sealed class PriceHistoryStore(KapeaDbContext context) : IPriceHistoryStore
{
    public async Task<IReadOnlyList<DailyPrice>> GetAsync(
        Guid assetId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default) =>
        await context.DailyPrices
            .Where(price => price.AssetId == assetId && price.Date >= from && price.Date <= to)
            .OrderBy(price => price.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>>> GetAsync(
        IReadOnlyCollection<Guid> assetIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<DailyPrice>>();
        }

        var prices = await context.DailyPrices
            .Where(price => assetIds.Contains(price.AssetId) && price.Date >= from && price.Date <= to)
            .OrderBy(price => price.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return prices
            .GroupBy(price => price.AssetId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<DailyPrice>)[.. group]);
    }

    public async Task<IReadOnlyDictionary<Guid, StoredRange>> GetStoredRangeAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, StoredRange>();
        }

        return await context.DailyPrices
            .Where(price => assetIds.Contains(price.AssetId))
            .GroupBy(price => price.AssetId)
            .Select(group => new
            {
                AssetId = group.Key,
                First = group.Min(price => price.Date),
                Last = group.Max(price => price.Date),
            })
            .ToDictionaryAsync(
                entry => entry.AssetId,
                entry => new StoredRange(entry.First, entry.Last),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, DailyPrice>> GetLastBeforeAsync(
        IReadOnlyCollection<Guid> assetIds,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, DailyPrice>();
        }

        // Una fila por activo y no la serie entera: sólo interesa el cierre más reciente
        // anterior a la fecha.
        var last = await context.DailyPrices
            .Where(price => assetIds.Contains(price.AssetId) && price.Date < date)
            .GroupBy(price => price.AssetId)
            .Select(group => group.OrderByDescending(price => price.Date).First())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return last.ToDictionary(price => price.AssetId);
    }

    public async Task<IReadOnlyDictionary<Guid, PriceHistoryReach>> GetReachAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, PriceHistoryReach>();
        }

        return await context.PriceHistoryReaches
            .Where(reach => assetIds.Contains(reach.AssetId))
            .ToDictionaryAsync(reach => reach.AssetId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RecordReachAsync(PriceHistoryReach reach, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reach);

        var stored = await context.PriceHistoryReaches
            .SingleOrDefaultAsync(entry => entry.AssetId == reach.AssetId, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            context.PriceHistoryReaches.Add(reach);
        }
        else
        {
            // Cuando se preguntó por otro activo del proveedor, lo anterior no dice nada
            // de lo que ahora se pide: se reemplaza en lugar de ampliarse.
            var merged = stored.AnswersFor(reach.RequestedWith)
                ? stored.Including(reach.RequestedFrom, reach.RequestedTo)
                : reach;

            context.Entry(stored).CurrentValues.SetValues(merged);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Guarda la serie descargada.
    /// </summary>
    /// <remarks>
    /// Un día que ya está se deja como está en lugar de reescribirlo: dos proveedores
    /// dan cifras que difieren en el último decimal, y reescribir haría que el mismo día
    /// cambiara de valor según quién sincronizara el último, sin que nada lo explicara.
    /// </remarks>
    public async Task<int> UpsertAsync(IReadOnlyList<DailyPrice> prices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (prices.Count == 0)
        {
            return 0;
        }

        var assetIds = prices.Select(price => price.AssetId).Distinct().ToList();
        var earliest = prices.Min(price => price.Date);
        var latest = prices.Max(price => price.Date);

        var existing = await context.DailyPrices
            .Where(price => assetIds.Contains(price.AssetId) && price.Date >= earliest && price.Date <= latest)
            .Select(price => new { price.AssetId, price.Date })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stored = existing.Select(entry => (entry.AssetId, entry.Date)).ToHashSet();
        var written = 0;

        foreach (var price in prices)
        {
            if (!stored.Add((price.AssetId, price.Date)))
            {
                continue;
            }

            context.DailyPrices.Add(price);
            written++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return written;
    }
}
