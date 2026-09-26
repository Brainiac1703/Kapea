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
/// <param name="RequestedWith">
/// Identificador de proveedor con el que se preguntó, cuando lo había. Si el activo pasa
/// a resolverse con otro, lo pedido deja de valer: se está preguntando por otra cosa.
/// </param>
public sealed record PriceHistoryReach(
    Guid AssetId,
    DateOnly RequestedFrom,
    DateOnly RequestedTo,
    string? RequestedWith)
{
    /// <summary>Extiende lo pedido con un tramo nuevo, sin encoger nunca lo que ya abarcaba.</summary>
    public PriceHistoryReach Including(DateOnly from, DateOnly to) =>
        this with
        {
            RequestedFrom = from < RequestedFrom ? from : RequestedFrom,
            RequestedTo = to > RequestedTo ? to : RequestedTo,
        };

    /// <summary>Lo pedido sirve sólo si se preguntó por el mismo activo del proveedor.</summary>
    public bool AnswersFor(string? providerId) =>
        string.Equals(RequestedWith, providerId, StringComparison.Ordinal);

    public static PriceHistoryReach Of(Guid assetId, DateOnly from, DateOnly to, string? providerId) =>
        new(assetId, from, to, providerId);
}
