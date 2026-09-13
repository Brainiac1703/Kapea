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
