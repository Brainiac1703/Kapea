using Kapea.Application.Abstractions;
using Kapea.Domain.Indicators;
using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Strategies;

/// <summary>Lo que el motor necesita leer y escribir.</summary>
public interface IStrategyRepository
{
    Task<IReadOnlyList<Strategy>> ListAsync(CancellationToken cancellationToken = default);

    Task<Strategy?> FindAsync(Guid strategyId, CancellationToken cancellationToken = default);

    Task AddAsync(Strategy strategy, CancellationToken cancellationToken = default);

    /// <summary>Guarda las señales que aún no estaban, reconocidas por su huella.</summary>
    Task<int> AddSignalsAsync(IReadOnlyList<EmittedSignal> signals, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmittedSignal>> ListSignalsAsync(
        DateOnly from,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Un activo con su serie de precios, tal como lo consume el motor.</summary>
public sealed record PricedSeries(Guid AssetId, string CanonicalSymbol, IReadOnlyList<PricePoint> Series);

/// <summary>De dónde salen las series y lo que cuesta operar.</summary>
public interface IStrategyDataSource
{
    /// <summary>Series de los activos que el usuario vigila y que tengan precios.</summary>
    Task<IReadOnlyList<PricedSeries>> ListSeriesAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Coste de operar en la plataforma de un activo, o el declarado más caro.</summary>
    Task<TradingCost> CostAsync(CancellationToken cancellationToken = default);
}

/// <summary>Lo que dejó una pasada del motor.</summary>
public sealed record SignalRun(int Assets, int Emitted, int New);

/// <summary>
/// Ejecuta los sistemas declarados y guarda sus señales.
/// </summary>
/// <remarks>
/// Las señales se guardan en lugar de recalcularse al abrir la pantalla: una señal es un
/// hecho con fecha, y conservarla permite comparar después lo que el sistema dijo con lo
/// que se hizo.
/// </remarks>
public sealed class StrategyService(
    IStrategyRepository repository,
    IStrategyDataSource data,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<StrategyService> logger)
{
    public async Task<SignalRun> RunAsync(CancellationToken cancellationToken = default)
    {
        var strategies = await repository.ListAsync(cancellationToken).ConfigureAwait(false);

        if (strategies.Count == 0)
        {
            return new SignalRun(0, 0, 0);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Se pide tanto histórico como necesite el sistema más exigente, más un margen
        // para que su ventana esté completa desde el primer día evaluado. La misma cuenta
        // la usa la descarga de precios: si bajara menos, un activo recién añadido nunca
        // llegaría a evaluarse.
        var required = strategies.Max(strategy => strategy.Current.RequiredDays);
        var series = await data
            .ListSeriesAsync(StrategyLookback.From(today, required), today, cancellationToken)
            .ConfigureAwait(false);

        var emitted = new List<EmittedSignal>();

        foreach (var strategy in strategies)
        {
            var version = strategy.Current;

            foreach (var priced in series)
            {
                var signals = SignalEngine.Run(priced.AssetId, priced.Series, version);

                emitted.AddRange(signals.Select(signal =>
                    EmittedSignal.From(currentUser.Id, strategy.Id, version.Number, signal)));
            }
        }

        var stored = await repository.AddSignalsAsync(emitted, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Señales calculadas: {Emitidas} de {Sistemas} sistemas sobre {Activos} activos, {Nuevas} nuevas.",
            emitted.Count, strategies.Count, series.Count, stored);

        return new SignalRun(series.Count, emitted.Count, stored);
    }

    /// <summary>Simula un sistema sobre un activo con el coste real de operar.</summary>
    public async Task<BacktestResult?> SimulateAsync(
        Guid strategyId,
        Guid assetId,
        Money capital,
        int? versionNumber = null,
        CancellationToken cancellationToken = default)
    {
        var strategy = await repository.FindAsync(strategyId, cancellationToken).ConfigureAwait(false);

        if (strategy is null)
        {
            return null;
        }

        // La versión pedida y no la vigente: simular una versión antigua tiene que usar
        // sus reglas, o la comparación entre versiones no diría nada.
        var version = versionNumber is { } number ? strategy.FindVersion(number) : strategy.Current;

        if (version is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var series = await data
            .ListSeriesAsync(today.AddDays(-3650), today, cancellationToken)
            .ConfigureAwait(false);

        var priced = series.FirstOrDefault(entry => entry.AssetId == assetId);

        if (priced is null || priced.Series.Count == 0)
        {
            return null;
        }

        var cost = await data.CostAsync(cancellationToken).ConfigureAwait(false);

        return Backtest.Run(assetId, priced.Series, version, capital, cost, SavingsTax.Spain2026);
    }
}
