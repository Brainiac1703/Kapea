using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>Cada cuánto se agrupa la serie para mirarla.</summary>
public enum SeriesInterval
{
    /// <summary>Un punto por día, que es como se guarda.</summary>
    Daily,

    /// <summary>Un punto por semana, de lunes a domingo.</summary>
    Weekly,

    /// <summary>Un punto por mes natural.</summary>
    Monthly,
}

/// <summary>
/// Agrupa la serie de un activo en tramos de una semana o de un mes.
/// </summary>
/// <remarks>
/// Cinco años en días son más puntos que píxeles y se pisan entre sí. Agregando se ve la
/// forma, y a diferencia de reducir para dibujar, agregar conserva los extremos: una vela
/// semanal lleva el máximo de la semana, y un muestreo se lo saltaría.
/// </remarks>
public static class SeriesAggregation
{
    /// <summary>
    /// La serie agrupada por el intervalo pedido.
    /// </summary>
    /// <remarks>
    /// Cada tramo lleva la apertura del primer día con dato, el cierre del último, y los
    /// extremos de todos. Un tramo sin ningún día con dato no se inventa.
    /// </remarks>
    public static IReadOnlyList<AssetHistoryDay> By(
        IReadOnlyList<AssetHistoryDay> days,
        SeriesInterval interval)
    {
        ArgumentNullException.ThrowIfNull(days);

        if (interval == SeriesInterval.Daily || days.Count == 0)
        {
            return days;
        }

        var aggregated = new List<AssetHistoryDay>();

        foreach (var group in days.OrderBy(day => day.Date).GroupBy(day => Start(day.Date, interval)))
        {
            var withPrice = group.Where(day => day.PriceInEuros is not null).ToList();

            if (withPrice.Count == 0)
            {
                continue;
            }

            var last = withPrice[^1];

            aggregated.Add(last with
            {
                // La fecha del tramo es la de su primer día: es donde empieza en el eje.
                Date = group.Key,

                // El máximo del tramo es el mayor de los máximos de sus días, no el
                // mayor de sus cierres. Confundirlos deja los extremos sistemáticamente
                // por debajo de la realidad, y es el error clásico al agregar velas.
                OpenInEuros = withPrice[0].OpenInEuros ?? withPrice[0].PriceInEuros,
                HighInEuros = Extreme(withPrice, highest: true),
                LowInEuros = Extreme(withPrice, highest: false),
            });
        }

        return aggregated;
    }

    /// <summary>El día en que empieza el tramo al que pertenece una fecha.</summary>
    public static DateOnly Start(DateOnly date, SeriesInterval interval) => interval switch
    {
        SeriesInterval.Weekly => date.AddDays(-((int)date.DayOfWeek + 6) % 7),
        SeriesInterval.Monthly => new DateOnly(date.Year, date.Month, 1),
        _ => date,
    };

    /// <summary>El intervalo que conviene para un número de días, si nadie dice otra cosa.</summary>
    /// <remarks>
    /// Hasta medio año se ve bien día a día. A partir de dos años, los días se pisan tanto
    /// que la forma se pierde. Es una sugerencia: quien quiera cinco años en días, puede.
    /// </remarks>
    public static SeriesInterval Suggested(int days) => days switch
    {
        <= 190 => SeriesInterval.Daily,
        <= 760 => SeriesInterval.Weekly,
        _ => SeriesInterval.Monthly,
    };

    /// <summary>
    /// El extremo del tramo, mirando el recorrido de cada día y su cierre.
    /// </summary>
    /// <remarks>
    /// Un día sin recorrido aporta su cierre, que es lo único que se sabe de él. Así un
    /// tramo con días de las dos clases sigue dando un extremo cierto en lugar de ninguno.
    /// </remarks>
    private static Money Extreme(IReadOnlyList<AssetHistoryDay> days, bool highest)
    {
        var amounts = days.Select(day =>
            (highest ? day.HighInEuros : day.LowInEuros) ?? day.PriceInEuros!.Value);

        return highest
            ? amounts.MaxBy(amount => amount.Amount)
            : amounts.MinBy(amount => amount.Amount);
    }
}
