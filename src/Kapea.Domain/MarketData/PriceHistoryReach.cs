namespace Kapea.Domain.MarketData;

/// <summary>
/// Hasta dónde se ha pedido la serie de un activo, se haya obtenido algo o no.
/// </summary>
/// <remarks>
/// No es lo mismo que lo guardado, y por eso hace falta guardarlo aparte. Un activo que
/// empezó a cotizar en 2021 al que se le pide desde 2000 devuelve su primera cotización
/// y nada antes; mirando sólo la serie, el tramo anterior seguiría pareciendo un hueco
/// por rellenar y volvería a pedirse en cada ejecución, indefinidamente y sin que nada
/// lo delatara.
///
/// Es un catálogo global, como los precios: lo que se le pidió al proveedor por bitcoin
/// no depende de quién pregunte.
/// </remarks>
/// <param name="AssetId">Activo del catálogo.</param>
/// <param name="RequestedFrom">Día más antiguo por el que se ha preguntado.</param>
/// <param name="RequestedTo">Día más reciente por el que se ha preguntado.</param>
/// <param name="RangeRequested">
/// Ya se volvió a pedir la serie para completar su recorrido. Sin esta marca, el relleno
/// del recorrido se repetiría en cada vuelta: el tramo ya consta pedido, pero lo que se
/// pide ahora no es lo mismo que se pidió entonces.
/// </param>
/// <param name="RequestedWith">
/// Identificador de proveedor con el que se preguntó, cuando lo había. Si el activo pasa
/// a resolverse con otro, lo pedido deja de valer: se está preguntando por otra cosa.
/// </param>
public sealed record PriceHistoryReach(
    Guid AssetId,
    DateOnly RequestedFrom,
    DateOnly RequestedTo,
    string? RequestedWith,
    bool RangeRequested = false)
{
    /// <summary>
    /// Extiende lo pedido con otra petición, sin encoger nunca lo que ya abarcaba.
    /// </summary>
    /// <remarks>
    /// La marca del recorrido se conserva si la tenía cualquiera de los dos. Quedarse con
    /// la del guardado descartaría la de la petición que acaba de completarlo, y el
    /// relleno del recorrido se repetiría en cada vuelta.
    /// </remarks>
    public PriceHistoryReach Including(PriceHistoryReach other) =>
        this with
        {
            RequestedFrom = other.RequestedFrom < RequestedFrom ? other.RequestedFrom : RequestedFrom,
            RequestedTo = other.RequestedTo > RequestedTo ? other.RequestedTo : RequestedTo,
            RangeRequested = RangeRequested || other.RangeRequested,
        };

    /// <summary>Extiende lo pedido con un tramo nuevo.</summary>
    public PriceHistoryReach Including(DateOnly from, DateOnly to) =>
        this with
        {
            RequestedFrom = from < RequestedFrom ? from : RequestedFrom,
            RequestedTo = to > RequestedTo ? to : RequestedTo,
        };

    /// <summary>Lo pedido sirve sólo si se preguntó por el mismo activo del proveedor.</summary>
    public bool AnswersFor(string? providerId) =>
        string.Equals(RequestedWith, providerId, StringComparison.Ordinal);

    /// <summary>
    /// Un alcance recién nacido, de una serie que se acaba de pedir.
    /// </summary>
    /// <remarks>
    /// Nace con el recorrido dado por pedido: lo que se acaba de descargar ya trae el
    /// que su proveedor dé. La marca en falso está reservada a lo que se descargó antes
    /// de que el recorrido se guardara, que es lo único que hay que volver a pedir.
    /// </remarks>
    public static PriceHistoryReach Of(Guid assetId, DateOnly from, DateOnly to, string? providerId) =>
        new(assetId, from, to, providerId, RangeRequested: true);
}
