using Kapea.Application.Strategies;
using Kapea.Domain.Strategies;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>Sistemas y señales sobre EF Core.</summary>
public sealed class StrategyStore(KapeaDbContext context) : IStrategyRepository
{
    public async Task<IReadOnlyList<Strategy>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Strategies
            .OrderBy(strategy => strategy.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Strategy?> FindAsync(Guid strategyId, CancellationToken cancellationToken = default) =>
        await context.Strategies
            .FirstOrDefaultAsync(strategy => strategy.Id == strategyId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        await context.Strategies.AddAsync(strategy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Guarda solo las señales que aún no estaban.
    /// </summary>
    /// <remarks>
    /// Se comprueba antes de insertar en lugar de dejar que falle la clave: una tanda
    /// trae cientos de señales ya conocidas y una sola repetida abortaría el guardado de
    /// todas las demás.
    /// </remarks>
    public async Task<int> AddSignalsAsync(
        IReadOnlyList<EmittedSignal> signals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.Count == 0)
        {
            return 0;
        }

        var fingerprints = signals.Select(signal => signal.Fingerprint).Distinct().ToList();

        var known = await context.EmittedSignals
            .Where(signal => fingerprints.Contains(signal.Fingerprint))
            .Select(signal => signal.Fingerprint)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stored = known.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var signal in signals)
        {
            if (!stored.Add(signal.Fingerprint))
            {
                continue;
            }

            context.EmittedSignals.Add(signal);
            added++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return added;
    }

    public async Task<IReadOnlyList<EmittedSignal>> ListSignalsAsync(
        DateOnly from,
        CancellationToken cancellationToken = default) =>
        await context.EmittedSignals
            .Where(signal => signal.Date >= from)
            .OrderByDescending(signal => signal.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Las series y el coste de operar, leídos de la base de datos.
/// </summary>
/// <remarks>
/// El coste que se aplica es el más alto de las plataformas donde el usuario opera. Es
/// deliberadamente conservador: una simulación que use el coste más bajo hace parecer
/// viables sistemas que no lo son en la plataforma donde de verdad se operaría.
/// </remarks>
public sealed class StrategyDataSource(KapeaDbContext context, Application.Abstractions.IPriceHistoryStore prices)
    : IStrategyDataSource
{
    public async Task<IReadOnlyList<PricedSeries>> ListSeriesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Sólo lo que el usuario vigila: lo que ha añadido y lo que tiene. Evaluar todo
        // el catálogo emitía señales de activos que a él no le interesan, y no emitía
        // ninguna de aquello en lo que está pensando entrar y aún no ha comprado.
        var watched = await context.WatchedAssets
            .Select(entry => entry.AssetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var lots = await context.Lots
            .Select(lot => lot.AssetId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var followed = watched.Concat(lots).ToHashSet();

        var assets = await context.Assets
            .Where(asset => followed.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.CanonicalSymbol })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var series = await prices
            .GetAsync([.. assets.Select(asset => asset.Id)], from, to, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. assets
                .Where(asset => series.ContainsKey(asset.Id))
                .Select(asset => new PricedSeries(
                    asset.Id,
                    asset.CanonicalSymbol,
                    [
                        .. series[asset.Id]
                            .OrderBy(price => price.Date)
                            .Select(price => new Domain.Indicators.PricePoint(price.Date, price.PriceInEuros)),
                    ]))
                .Where(priced => priced.Series.Count > 0),
        ];
    }

    public async Task<Domain.Strategies.TradingCost> CostAsync(CancellationToken cancellationToken = default)
    {
        var platforms = await context.Accounts
            .Select(account => account.Platform)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fees = await context.Platforms
            .Where(platform => platforms.Contains(platform.Code))
            .Select(platform => new { platform.FeeRate, platform.FixedFee })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (fees.Count == 0)
        {
            return Domain.Strategies.TradingCost.None;
        }

        return new Domain.Strategies.TradingCost(
            fees.Max(fee => fee.FeeRate),
            Domain.ValueObjects.Money.Euros(fees.Max(fee => fee.FixedFee)));
    }
}
