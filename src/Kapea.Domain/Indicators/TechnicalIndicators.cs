namespace Kapea.Domain.Indicators;

/// <summary>Un valor de un indicador, con el día al que corresponde.</summary>
public sealed record IndicatorPoint(DateOnly Date, decimal Value);

/// <summary>Un día de la serie sobre la que se calculan los indicadores.</summary>
public sealed record PricePoint(DateOnly Date, decimal PriceInEuros);

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
