namespace Kapea.Client.Layout;

/// <summary>
/// Las direcciones que una pantalla enlaza a otra.
/// </summary>
/// <remarks>
/// Construirlas en un solo sitio evita lo que ya pasó: la lista de seguimiento enlazaba
/// a «evolution?asset=…» cuando la página del activo está en «evolution/{id}», así que
/// la dirección casaba con la del patrimonio y la pregunta se ignoraba. El enlace no
/// fallaba, llevaba a otro sitio, que es peor.
/// </remarks>
public static class Routes
{
    /// <summary>La evolución del patrimonio entero.</summary>
    public const string PortfolioChart = "evolution";

    /// <summary>La evolución de un activo, con su precio y sus indicadores.</summary>
    public static string AssetChart(Guid assetId) => $"{PortfolioChart}/{assetId}";
}
