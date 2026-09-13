using Kapea.Domain.Indicators;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Strategies;

/// <summary>Qué propone una señal.</summary>
public enum SignalDirection
{
    Entry = 1,

    Exit = 2,
}

/// <summary>
/// Una señal emitida por un sistema sobre un activo y un día.
/// </summary>
/// <param name="Reason">Qué condición se cumplió y con qué valores.</param>
/// <param name="Target">Objetivo de precio, si el sistema lo declara.</param>
/// <param name="StopLoss">Nivel de salida, si el sistema lo declara.</param>
public sealed record Signal(
    Guid AssetId,
    DateOnly Date,
    SignalDirection Direction,
    Money PriceInEuros,
    string Reason,
    Money? Target = null,
    Money? StopLoss = null);

/// <summary>
/// Recorre la serie y emite las señales de un sistema.
/// </summary>
/// <remarks>
/// El recorrido es hacia delante y cada día se decide solo con lo que se sabía ese día.
/// Por eso evaluar el histórico entero da exactamente las mismas señales que se habrían
/// emitido en su momento, y por eso el simulador puede usar este mismo motor.
///
/// Una entrada no se repite mientras la posición siga abierta: un sistema cuya condición
/// se cumple veinte días seguidos propone una compra, no veinte.
/// </remarks>
public static class SignalEngine
{
    public static IReadOnlyList<Signal> Run(
        Guid assetId,
        IReadOnlyList<PricePoint> series,
        StrategyVersion version)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(version);

        var indicators = IndicatorSet.For(series, version);
        var signals = new List<Signal>();
        var open = false;
        var entryPrice = 0m;
        var stop = 0m;
        var target = 0m;

        foreach (var point in series)
        {
            if (!open)
            {
                var entry = StrategyEvaluator.Evaluate(version.Entry, indicators, point.Date);

                if (entry.Met != true)
                {
                    continue;
                }

                entryPrice = point.PriceInEuros;
                stop = StopFor(version, indicators, point);
                target = TargetFor(version, entryPrice, stop, indicators, point);
                open = true;

                signals.Add(new Signal(
                    assetId,
                    point.Date,
                    SignalDirection.Entry,
                    Money.Euros(entryPrice),
                    entry.Reason,
                    target == 0m ? null : Money.Euros(target),
                    stop == 0m ? null : Money.Euros(stop)));

                continue;
            }

            var reason = Closes(version, indicators, point, stop, target);

            if (reason is null)
            {
                continue;
            }

            open = false;

            signals.Add(new Signal(
                assetId, point.Date, SignalDirection.Exit, Money.Euros(point.PriceInEuros), reason));
        }

        return signals;
    }

    /// <summary>Por qué se cierra ese día, o nada si no se cierra.</summary>
    private static string? Closes(
        StrategyVersion version,
        IndicatorSet indicators,
        PricePoint point,
        decimal stop,
        decimal target)
    {
        // El nivel de salida se mira antes que el objetivo: si el día tocó los dos, dar
        // por bueno el objetivo sería contarse una historia favorable que no se puede
        // sostener con un cierre diario.
        if (stop > 0m && point.PriceInEuros <= stop)
        {
            return $"El precio ({point.PriceInEuros:0.####}) ha llegado al nivel de salida ({stop:0.####}).";
        }

        if (target > 0m && point.PriceInEuros >= target)
        {
            return $"El precio ({point.PriceInEuros:0.####}) ha llegado al objetivo ({target:0.####}).";
        }

        if (version.Exit is { } exit)
        {
            var evaluation = StrategyEvaluator.Evaluate(exit, indicators, point.Date);

            if (evaluation.Met == true)
            {
                return evaluation.Reason;
            }
        }

        return null;
    }

    private static decimal StopFor(StrategyVersion version, IndicatorSet indicators, PricePoint point) =>
        version.StopLoss switch
        {
            null => 0m,
            { Kind: LevelKind.Percentage } level => point.PriceInEuros * (1m - level.Factor),
            { Kind: LevelKind.RangeMultiple } level =>
                Range(indicators, point) is { } range ? point.PriceInEuros - (level.Factor * range) : 0m,

            // Un nivel de salida medido en múltiplos del riesgo sería circular: el riesgo
            // es justamente la distancia hasta él.
            _ => 0m,
        };

    private static decimal TargetFor(
        StrategyVersion version,
        decimal entryPrice,
        decimal stop,
        IndicatorSet indicators,
        PricePoint point) => version.Target switch
    {
        null => 0m,
        { Kind: LevelKind.Percentage } level => entryPrice * (1m + level.Factor),
        { Kind: LevelKind.RangeMultiple } level =>
            Range(indicators, point) is { } range ? entryPrice + (level.Factor * range) : 0m,
        { Kind: LevelKind.RiskMultiple } level when stop > 0m =>
            entryPrice + (level.Factor * (entryPrice - stop)),
        _ => 0m,
    };

    /// <summary>
    /// Lo que el activo se mueve en un día, para los niveles que se miden en múltiplos.
    /// </summary>
    /// <remarks>
    /// Catorce días es la ventana habitual. Si no hay bastante histórico, el nivel se
    /// queda sin fijar en lugar de calcularse sobre una ventana a medias.
    /// </remarks>
    private static decimal? Range(IndicatorSet indicators, PricePoint point) =>
        indicators.Value(Term.Indicator(Operand.AverageDailyRange, 14), point.Date)
        ?? TechnicalIndicators.AverageDailyRange(indicators.Series)
            .FirstOrDefault(value => value.Date == point.Date)?.Value;
}
