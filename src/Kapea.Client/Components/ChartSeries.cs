namespace Kapea.Client.Components;

/// <summary>
/// Un punto de una serie. Sin valor, ese día no se dibuja.
/// </summary>
/// <remarks>
/// La ausencia viaja como nulo y no como cero: un día sin precio dibujado a cero se lee
/// como una caída a nada, que es lo contrario de lo que pasó.
/// </remarks>
public sealed record ChartPoint(DateOnly Date, decimal? Value);

/// <summary>Una línea de la gráfica, con su nombre y su color.</summary>
/// <param name="Dashed">Las líneas de apoyo, como las medias, van punteadas.</param>
public sealed record ChartLine(string Name, IReadOnlyList<ChartPoint> Points, string Color, bool Dashed = false)
{
    /// <summary>Colores de las series, por orden. Distinguibles también en blanco y negro.</summary>
    public static readonly string[] Palette =
    [
        "#2563eb",
        "#d97706",
        "#059669",
        "#9333ea",
        "#dc2626",
        "#0891b2",
    ];

    public static string ColorAt(int index) => Palette[index % Palette.Length];
}
