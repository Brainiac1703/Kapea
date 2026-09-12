namespace Kapea.Domain.Indicators;

/// <summary>Un valor de un indicador, con el día al que corresponde.</summary>
public sealed record IndicatorPoint(DateOnly Date, decimal Value);

/// <summary>Un día de la serie sobre la que se calculan los indicadores.</summary>
public sealed record PricePoint(DateOnly Date, decimal PriceInEuros);

/// <summary>Un día del MACD.</summary>
/// <param name="Distance">Línea menos señal. El cruce es el día en que cambia de signo.</param>
public sealed record MacdPoint(DateOnly Date, decimal Line, decimal Signal, decimal Distance);

/// <summary>Un día de las bandas de volatilidad.</summary>
public sealed record BandPoint(DateOnly Date, decimal Middle, decimal Upper, decimal Lower);

/// <summary>
/// Indicadores sobre una serie de precios.
/// </summary>
/// <remarks>
/// Son funciones puras sobre los días que tienen precio, en su orden. Los días sin
/// precio no se rellenan: una media calculada sobre huecos rellenados dice lo que dice
/// el relleno, no lo que hizo el mercado. Por eso cada valor viaja con su fecha, y no
/// como una posición en una lista que habría que volver a cuadrar.
/// </remarks>
public static class TechnicalIndicators
{
    /// <summary>
    /// Media móvil simple de una ventana de días.
    /// </summary>
    /// <remarks>
    /// Los días anteriores a completar la ventana no tienen valor. Promediar lo que hay
    /// daría una media de cinco días llamándola de veinte.
    /// </remarks>
    public static IReadOnlyList<IndicatorPoint> SimpleMovingAverage(IReadOnlyList<PricePoint> series, int days)
    {
        EnsureWindow(series, days);

        var points = new List<IndicatorPoint>();

        if (series.Count < days)
        {
            return points;
        }

        var running = 0m;

        for (var index = 0; index < series.Count; index++)
        {
            running += series[index].PriceInEuros;

            if (index >= days)
            {
                running -= series[index - days].PriceInEuros;
            }

            if (index >= days - 1)
            {
                points.Add(new IndicatorPoint(series[index].Date, running / days));
            }
        }

        return points;
    }

    /// <summary>
    /// Media móvil exponencial.
    /// </summary>
    /// <remarks>
    /// Arranca de la media simple de la primera ventana, que es la convención habitual:
    /// empezar del primer precio haría que los primeros valores dependieran de un solo
    /// día y tardaran semanas en dejar de notarse.
    /// </remarks>
    public static IReadOnlyList<IndicatorPoint> ExponentialMovingAverage(IReadOnlyList<PricePoint> series, int days)
    {
        EnsureWindow(series, days);

        var points = new List<IndicatorPoint>();

        if (series.Count < days)
        {
            return points;
        }

        var smoothing = 2m / (days + 1);
        var average = series.Take(days).Sum(point => point.PriceInEuros) / days;

        points.Add(new IndicatorPoint(series[days - 1].Date, average));

        for (var index = days; index < series.Count; index++)
        {
            average += smoothing * (series[index].PriceInEuros - average);
            points.Add(new IndicatorPoint(series[index].Date, average));
        }

        return points;
    }

    /// <summary>
    /// Índice de fuerza relativa, entre 0 y 100.
    /// </summary>
    /// <remarks>
    /// Con el suavizado de Wilder, que es el original y el que usan las plataformas: una
    /// media simple de las variaciones da otra cifra y no se podría contrastar con nada.
    /// </remarks>
    public static IReadOnlyList<IndicatorPoint> RelativeStrengthIndex(IReadOnlyList<PricePoint> series, int days)
    {
        EnsureWindow(series, days);

        var points = new List<IndicatorPoint>();

        if (series.Count <= days)
        {
            return points;
        }

        var gains = 0m;
        var losses = 0m;

        for (var index = 1; index <= days; index++)
        {
            var change = series[index].PriceInEuros - series[index - 1].PriceInEuros;

            gains += Math.Max(change, 0m);
            losses += Math.Max(-change, 0m);
        }

        gains /= days;
        losses /= days;

        points.Add(new IndicatorPoint(series[days].Date, Strength(gains, losses)));

        for (var index = days + 1; index < series.Count; index++)
        {
            var change = series[index].PriceInEuros - series[index - 1].PriceInEuros;

            gains = ((gains * (days - 1)) + Math.Max(change, 0m)) / days;
            losses = ((losses * (days - 1)) + Math.Max(-change, 0m)) / days;

            points.Add(new IndicatorPoint(series[index].Date, Strength(gains, losses)));
        }

        return points;
    }

    /// <summary>
    /// Convergencia y divergencia de medias móviles.
    /// </summary>
    /// <remarks>
    /// La línea principal es la diferencia entre dos medias exponenciales; la de señal es
    /// la media exponencial de esa diferencia. Lo que se mira es cuándo se cruzan, y por
    /// eso se devuelve también la distancia entre ambas: el cruce es el día en que cambia
    /// de signo.
    /// </remarks>
    public static IReadOnlyList<MacdPoint> Macd(
        IReadOnlyList<PricePoint> series,
        int fastDays = 12,
        int slowDays = 26,
        int signalDays = 9)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfLessThan(fastDays, 2);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(slowDays, fastDays);
        ArgumentOutOfRangeException.ThrowIfLessThan(signalDays, 2);

        var fast = ExponentialMovingAverage(series, fastDays).ToDictionary(point => point.Date, point => point.Value);
        var slow = ExponentialMovingAverage(series, slowDays);

        // La línea solo existe donde existen las dos medias, que es desde que la lenta
        // completa su ventana.
        var line = slow
            .Where(point => fast.ContainsKey(point.Date))
            .Select(point => new PricePoint(point.Date, fast[point.Date] - point.Value))
            .ToList();

        var signal = ExponentialMovingAverage(line, signalDays).ToDictionary(point => point.Date, point => point.Value);

        return
        [
            .. line
                .Where(point => signal.ContainsKey(point.Date))
                .Select(point => new MacdPoint(
                    point.Date,
                    point.PriceInEuros,
                    signal[point.Date],
                    point.PriceInEuros - signal[point.Date])),
        ];
    }

    /// <summary>
    /// Bandas de volatilidad alrededor de la media.
    /// </summary>
    /// <remarks>
    /// Dicen si un precio está caro o barato respecto a sí mismo, que es distinto de
    /// estar caro en términos absolutos. La desviación se calcula sobre la misma ventana
    /// de la media, con el denominador de la población: es la convención del indicador y
    /// cambiarla daría bandas que no coinciden con las de ninguna plataforma.
    /// </remarks>
    public static IReadOnlyList<BandPoint> BollingerBands(
        IReadOnlyList<PricePoint> series,
        int days = 20,
        decimal deviations = 2m)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deviations);

        var points = new List<BandPoint>();

        for (var index = days - 1; index < series.Count; index++)
        {
            var window = series.Skip(index - days + 1).Take(days).Select(point => point.PriceInEuros).ToList();
            var average = window.Sum() / days;
            var variance = window.Sum(price => (price - average) * (price - average)) / days;
            var deviation = (decimal)Math.Sqrt((double)variance);

            points.Add(new BandPoint(
                series[index].Date,
                average,
                average + (deviations * deviation),
                average - (deviations * deviation)));
        }

        return points;
    }

    /// <summary>
    /// Cuánto se mueve un activo de un cierre al siguiente, en media.
    /// </summary>
    /// <remarks>
    /// Es lo que convierte un nivel de salida en una decisión calculada en lugar de un
    /// número redondo: dos activos con el mismo capital asignado necesitan distancias
    /// distintas si uno se mueve el doble que el otro.
    ///
    /// Se calcula sobre cierres porque la serie guardada solo tiene cierres. No todos los
    /// proveedores dan el rango del día, y usarlo dejaría el indicador disponible para
    /// unos activos y no para otros.
    /// </remarks>
    public static IReadOnlyList<IndicatorPoint> AverageDailyRange(IReadOnlyList<PricePoint> series, int days = 14)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 2);

        var points = new List<IndicatorPoint>();

        if (series.Count <= days)
        {
            return points;
        }

        var moves = new List<decimal>(series.Count - 1);

        for (var index = 1; index < series.Count; index++)
        {
            moves.Add(Math.Abs(series[index].PriceInEuros - series[index - 1].PriceInEuros));
        }

        // Suavizado de Wilder, igual que en la fuerza relativa, para que las dos cifras
        // se muevan al mismo ritmo.
        var average = moves.Take(days).Sum() / days;

        points.Add(new IndicatorPoint(series[days].Date, average));

        for (var index = days; index < moves.Count; index++)
        {
            average = ((average * (days - 1)) + moves[index]) / days;
            points.Add(new IndicatorPoint(series[index + 1].Date, average));
        }

        return points;
    }

    /// <summary>Una serie que solo sube vale 100; una que solo baja, 0.</summary>
    private static decimal Strength(decimal gains, decimal losses) => losses == 0m
        ? gains == 0m ? 50m : 100m
        : 100m - (100m / (1m + (gains / losses)));

    private static void EnsureWindow(IReadOnlyList<PricePoint> series, int days)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 2);
    }
}
