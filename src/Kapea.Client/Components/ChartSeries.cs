namespace Kapea.Client.Components;

/// <summary>
/// Un punto de una serie. Sin valor, ese día no se dibuja.
/// </summary>
/// <remarks>
/// La ausencia viaja como nulo y no como cero: un día sin precio dibujado a cero se lee
/// como una caída a nada, que es lo contrario de lo que pasó.
/// </remarks>
public sealed record ChartPoint(DateOnly Date, decimal? Value);

/// <summary>Una línea de la gráfica, con su nombre y la serie que le da color.</summary>
/// <param name="Series">
/// Clase CSS de la serie. El color vive en la hoja de estilos y no aquí, porque cambia
/// con el modo oscuro y un color fijo en el SVG no se enteraría.
/// </param>
/// <param name="Dashed">Las líneas de apoyo, como las medias, van punteadas.</param>
public sealed record ChartLine(string Name, IReadOnlyList<ChartPoint> Points, string Series, bool Dashed = false)
{
    /// <summary>Series con color propio, comprobadas para daltonismo en ese orden.</summary>
    public const int DistinctSeries = 5;

    /// <summary>
    /// La serie de la posición dada.
    /// </summary>
    /// <remarks>
    /// A partir de la quinta no se repite un color: dos líneas del mismo color se leen
    /// como la misma cosa. Las que sobran van en gris, como «otras».
    /// </remarks>
    public static string SeriesAt(int index) =>
        index is >= 0 and < DistinctSeries ? $"series-{index}" : "series-other";
}

/// <summary>
/// Cuántos puntos de una serie se llegan a dibujar.
/// </summary>
/// <remarks>
/// El dibujo mide mil unidades de ancho, así que más de mil puntos se pisan entre sí.
/// Sin tope, veintiséis años de una acción salían en más de mil polilíneas —una por cada
/// fin de semana— y la pantalla tardaba decenas de segundos en responder para distinguir
/// posiciones que no llegan a un píxel.
///
/// Se reduce lo que se pinta, nunca lo que se calcula ni lo que se lee al pasar el ratón:
/// una media no puede cambiar porque la gráfica dibuje menos puntos.
/// </remarks>
public static class ChartSampling
{
    /// <summary>
    /// Los puntos que se dibujan, agrupados por tramos consecutivos si son demasiados.
    /// </summary>
    /// <remarks>
    /// De cada tramo sale un punto con valor, si lo hay. Un tramo entero sin valor sigue
    /// siendo un hueco: así una serie que se interrumpió de verdad se ve cortada, y un
    /// fin de semana suelto deja de partir la línea cuando se miran años.
    /// </remarks>
    public static IReadOnlyList<ChartPoint> Reduce(IReadOnlyList<ChartPoint> points, int maximum)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);

        if (points.Count <= maximum)
        {
            return points;
        }

        var step = (int)Math.Ceiling((double)points.Count / maximum);
        var drawn = new List<ChartPoint>((points.Count / step) + 1);

        for (var index = 0; index < points.Count; index += step)
        {
            var chosen = points[index];

            for (var offset = 0; offset < step && index + offset < points.Count; offset++)
            {
                if (points[index + offset].Value is not null)
                {
                    chosen = points[index + offset];

                    break;
                }
            }

            drawn.Add(chosen);
        }

        return drawn;
    }
}
