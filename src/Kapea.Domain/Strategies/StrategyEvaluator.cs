using Kapea.Domain.Indicators;

namespace Kapea.Domain.Strategies;

/// <summary>
/// Los valores de un día sobre los que se evalúa una condición.
/// </summary>
/// <remarks>
/// Se precalculan todos los indicadores que el sistema nombra y se consultan por día. Una
/// condición no puede pedir un indicador que no esté aquí: eso se rechaza al guardar el
/// sistema, no al evaluarlo.
/// </remarks>
public sealed class IndicatorSet
{
    private readonly Dictionary<(Operand Operand, int Window), Dictionary<DateOnly, decimal>> _values = [];

    private IndicatorSet(IReadOnlyList<PricePoint> series) => Series = series;

    public IReadOnlyList<PricePoint> Series { get; }

    /// <summary>Precalcula lo que el sistema necesita y nada más.</summary>
    public static IndicatorSet For(IReadOnlyList<PricePoint> series, StrategyVersion version)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(version);

        var set = new IndicatorSet(series);

        foreach (var (operand, window) in Needed(version))
        {
            set._values[(operand, window)] = Compute(series, operand, window);
        }

        return set;
    }

    /// <summary>Valor del indicador ese día, o nada si ese día no lo tiene.</summary>
    public decimal? Value(Term term, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(term);

        if (term.Operand == Operand.Constant)
        {
            return term.Value;
        }

        if (term.Operand == Operand.Price)
        {
            return Series.FirstOrDefault(point => point.Date == date)?.PriceInEuros;
        }

        return _values.TryGetValue((term.Operand, term.Window ?? 0), out var series)
            && series.TryGetValue(date, out var value)
                ? value
                : null;
    }

    private static IEnumerable<(Operand Operand, int Window)> Needed(StrategyVersion version)
    {
        var terms = new List<Term>();

        Collect(version.Entry, terms);

        if (version.Exit is { } exit)
        {
            Collect(exit, terms);
        }

        return terms
            .Where(term => term.Operand is not (Operand.Constant or Operand.Price))
            .Select(term => (term.Operand, Window: term.Window ?? 0))
            .Distinct();
    }

    private static void Collect(Condition condition, List<Term> terms)
    {
        if (condition.Junction == Junction.Comparison)
        {
            if (condition.Left is { } left)
            {
                terms.Add(left);
            }

            if (condition.Right is { } right)
            {
                terms.Add(right);
            }

            return;
        }

        foreach (var child in condition.Children ?? [])
        {
            Collect(child, terms);
        }
    }

    private static Dictionary<DateOnly, decimal> Compute(
        IReadOnlyList<PricePoint> series,
        Operand operand,
        int window) => operand switch
    {
        Operand.SimpleMovingAverage => Points(TechnicalIndicators.SimpleMovingAverage(series, window)),
        Operand.ExponentialMovingAverage => Points(TechnicalIndicators.ExponentialMovingAverage(series, window)),
        Operand.RelativeStrengthIndex => Points(TechnicalIndicators.RelativeStrengthIndex(series, window)),
        Operand.AverageDailyRange => Points(TechnicalIndicators.AverageDailyRange(series, window)),
        Operand.MacdLine => Macd(series, window, point => point.Line),
        Operand.MacdSignal => Macd(series, window, point => point.Signal),
        Operand.MacdDistance => Macd(series, window, point => point.Distance),
        Operand.BollingerUpper => Bands(series, window, point => point.Upper),
        Operand.BollingerMiddle => Bands(series, window, point => point.Middle),
        Operand.BollingerLower => Bands(series, window, point => point.Lower),
        _ => [],
    };

    private static Dictionary<DateOnly, decimal> Points(IReadOnlyList<IndicatorPoint> points) =>
        points.ToDictionary(point => point.Date, point => point.Value);

    /// <summary>
    /// El MACD con la ventana declarada como lenta.
    /// </summary>
    /// <remarks>
    /// Sus proporciones habituales son doce, veintiséis y nueve. Se derivan de la ventana
    /// para que una regla no tenga que declarar tres números, y se redondean hacia arriba
    /// para que ninguna quede por debajo de dos días.
    /// </remarks>
    private static Dictionary<DateOnly, decimal> Macd(
        IReadOnlyList<PricePoint> series,
        int slow,
        Func<MacdPoint, decimal> select)
    {
        var fast = Math.Max(2, (int)Math.Ceiling(slow * 12d / 26d));
        var signal = Math.Max(2, (int)Math.Ceiling(slow * 9d / 26d));

        return TechnicalIndicators.Macd(series, fast, Math.Max(fast + 1, slow), signal)
            .ToDictionary(point => point.Date, select);
    }

    private static Dictionary<DateOnly, decimal> Bands(
        IReadOnlyList<PricePoint> series,
        int window,
        Func<BandPoint, decimal> select) =>
        TechnicalIndicators.BollingerBands(series, window).ToDictionary(point => point.Date, select);
}

/// <summary>Por qué una condición se cumplió o no.</summary>
/// <param name="Met">Si se cumplió. Nulo cuando no se pudo evaluar por falta de datos.</param>
public sealed record Evaluation(bool? Met, string Reason);

/// <summary>
/// Evalúa las condiciones de un sistema sobre un día concreto.
/// </summary>
/// <remarks>
/// Nunca mira más allá del día que se le pide, y para un cruce solo necesita el día
/// anterior. Es lo que garantiza que la señal de una fecha pasada sea la que se habría
/// emitido ese día.
/// </remarks>
public static class StrategyEvaluator
{
    public static Evaluation Evaluate(Condition condition, IndicatorSet indicators, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(indicators);

        return condition.Junction switch
        {
            Junction.Comparison => Compare(condition, indicators, date),
            Junction.All => Combine(condition, indicators, date, all: true),
            _ => Combine(condition, indicators, date, all: false),
        };
    }

    private static Evaluation Compare(Condition condition, IndicatorSet indicators, DateOnly date)
    {
        var left = indicators.Value(condition.Left!, date);
        var right = indicators.Value(condition.Right!, date);

        if (left is null || right is null)
        {
            return new Evaluation(null, $"Faltan datos para {condition.Describe()}.");
        }

        var comparison = condition.Comparison!.Value;

        if (comparison is Comparison.GreaterThan or Comparison.LessThan)
        {
            var met = comparison == Comparison.GreaterThan ? left > right : left < right;

            return new Evaluation(met, Reason(condition, left.Value, right.Value));
        }

        // Un cruce necesita el día anterior: sin él no se distingue de un estado que ya
        // venía dándose, y eso convertiría cada día en una señal.
        var previous = Previous(indicators, date);

        if (previous is null)
        {
            return new Evaluation(null, $"Falta el día anterior para {condition.Describe()}.");
        }

        var before = indicators.Value(condition.Left!, previous.Value);
        var beforeRight = indicators.Value(condition.Right!, previous.Value);

        if (before is null || beforeRight is null)
        {
            return new Evaluation(null, $"Faltan datos del día anterior para {condition.Describe()}.");
        }

        var crossed = comparison == Comparison.CrossesAbove
            ? before <= beforeRight && left > right
            : before >= beforeRight && left < right;

        return new Evaluation(crossed, Reason(condition, left.Value, right.Value));
    }

    private static Evaluation Combine(Condition condition, IndicatorSet indicators, DateOnly date, bool all)
    {
        var results = (condition.Children ?? []).Select(child => Evaluate(child, indicators, date)).ToList();

        if (results.Count == 0)
        {
            return new Evaluation(null, "La condición no tiene nada que evaluar.");
        }

        // Si falta un dato, no se puede afirmar ni negar: en «todas» basta que falte una,
        // y en «cualquiera» solo si ninguna se cumple por sí sola.
        if (all && results.Any(result => result.Met is null))
        {
            return new Evaluation(null, string.Join("; ", results.Where(r => r.Met is null).Select(r => r.Reason)));
        }

        if (!all && results.All(result => result.Met is not true) && results.Any(result => result.Met is null))
        {
            return new Evaluation(null, string.Join("; ", results.Where(r => r.Met is null).Select(r => r.Reason)));
        }

        var met = all ? results.All(result => result.Met == true) : results.Any(result => result.Met == true);

        return new Evaluation(met, string.Join(all ? " y " : " o ", results.Select(result => result.Reason)));
    }

    private static DateOnly? Previous(IndicatorSet indicators, DateOnly date)
    {
        DateOnly? previous = null;

        foreach (var point in indicators.Series)
        {
            if (point.Date >= date)
            {
                break;
            }

            previous = point.Date;
        }

        return previous;
    }

    private static string Reason(Condition condition, decimal left, decimal right) =>
        $"{condition.Left!.Describe()} ({left:0.####}) {condition.Comparison!.Value.Describe()} " +
        $"{condition.Right!.Describe()} ({right:0.####})";
}
