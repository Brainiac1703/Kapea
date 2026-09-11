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

    public async Task<IReadOnlyDictionary<Guid, DateOnly>> GetLastStoredDayAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assetIds);

        if (assetIds.Count == 0)
        {
            return new Dictionary<Guid, DateOnly>();
        }

        return await context.DailyPrices
            .Where(price => assetIds.Contains(price.AssetId))
            .GroupBy(price => price.AssetId)
            .Select(group => new { AssetId = group.Key, Last = group.Max(price => price.Date) })
            .ToDictionaryAsync(entry => entry.AssetId, entry => entry.Last, cancellationToken)
            .ConfigureAwait(false);
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
