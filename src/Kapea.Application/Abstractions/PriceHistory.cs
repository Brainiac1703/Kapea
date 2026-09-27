using Kapea.Domain.MarketData;

namespace Kapea.Application.Abstractions;

/// <summary>
/// La serie diaria de precios ya guardada.
/// </summary>
/// <remarks>
/// Es un puerto para que el cálculo de la evolución de la cartera no dependa de la base
/// de datos: recibe la serie resuelta, igual que el motor FIFO recibe los movimientos
/// ya valorados.
/// </remarks>
public interface IPriceHistoryStore
{
    /// <summary>Precios de un activo entre dos fechas, ambas incluidas, de la más antigua a la más reciente.</summary>
    Task<IReadOnlyList<DailyPrice>> GetAsync(
        Guid assetId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Precios de varios activos entre dos fechas, agrupados por activo.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>>> GetAsync(
        IReadOnlyCollection<Guid> assetIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Desde y hasta qué día llega la serie de cada activo, o su ausencia si no tiene.
    /// </summary>
    /// <remarks>
    /// Hacen falta los dos extremos y no solo el último: una serie que empezó tarde
    /// —porque cuando se descargó el proveedor no llegaba más atrás— tiene un hueco al
    /// principio que mirando solo el final no se vería nunca.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, StoredRange>> GetStoredRangeAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El último precio de cada activo anterior a una fecha, si lo hay.
    /// </summary>
    /// <remarks>
    /// Hace falta para valorar un día de mercado cerrado que caiga al principio del
    /// rango pedido: sin mirar antes de él no habría cierre del que tirar, y un rango
    /// que empezara en sábado saldría incompleto sin motivo.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, DailyPrice>> GetLastBeforeAsync(
        IReadOnlyCollection<Guid> assetIds,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hasta dónde se ha pedido la serie de cada activo, o su ausencia si nunca se pidió.
    /// </summary>
    /// <remarks>
    /// Va aparte del rango guardado porque responde a otra pregunta. El rango dice qué
    /// días tienen precio; esto dice por cuáles se preguntó. Un tramo que se pidió y no
    /// devolvió nada sólo aparece aquí, y es justo el que no hay que volver a pedir.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, PriceHistoryReach>> GetReachAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken = default);

    /// <summary>Deja constancia de un tramo pedido, ampliando el que ya hubiera.</summary>
    Task RecordReachAsync(PriceHistoryReach reach, CancellationToken cancellationToken = default);

    /// <summary>Guarda una serie descargada. Un día ya guardado no se duplica.</summary>
    Task<int> UpsertAsync(IReadOnlyList<DailyPrice> prices, CancellationToken cancellationToken = default);
}

/// <summary>Tramo de días que la serie de un activo ya cubre.</summary>
public sealed record StoredRange(DateOnly First, DateOnly Last);

/// <summary>Lo que hace falta para pedir la serie de un activo a un proveedor.</summary>
/// <param name="AssetId">Activo del catálogo, para devolver la serie ya atribuida.</param>
/// <param name="CanonicalSymbol">Símbolo del catálogo, que cada proveedor traduce al suyo.</param>
/// <param name="Class">Clase del activo: decide cómo se nombra en el proveedor.</param>
/// <param name="ProviderId">
/// Identificador con el que el proveedor conoce este activo, cuando se sabe. Manda
/// sobre cualquier traducción del símbolo: varios activos pueden compartir símbolo, y
/// deducirlo traería el precio de otra cosa sin que nada fallara.
/// </param>
public sealed record PriceHistoryRequest(
    Guid AssetId,
    string CanonicalSymbol,
    Kapea.Domain.Assets.AssetClass Class,
    DateOnly From,
    DateOnly To,
    string? ProviderId = null);

/// <summary>
/// Lo que un proveedor devuelve al pedirle un tramo de serie.
/// </summary>
/// <remarks>
/// No basta con la lista de precios. Una lista vacía significaba a la vez «aquí no hay
/// nada» y «no he podido preguntar», y quien decide si un tramo hay que volver a pedirlo
/// necesita distinguirlas: lo primero es una respuesta y no se repite, lo segundo es un
/// fallo pasajero que sí. Confundirlas convertía un 401 de un momento en un hueco
/// permanente.
///
/// Es un tipo y no un booleano suelto para que quepa el motivo del fallo el día que haga
/// falta, sin volver a tocar la firma del puerto.
/// </remarks>
/// <param name="Prices">Los días que el proveedor ha entregado, que pueden ser ninguno.</param>
/// <param name="Answered">
/// El proveedor pudo contestar. Falso sólo cuando falló: que conteste que no tiene nada
/// es una respuesta.
/// </param>
public sealed record PriceHistoryResult(IReadOnlyList<DailyPrice> Prices, bool Answered)
{
    /// <summary>Lo que el proveedor ha entregado, habiendo podido contestar.</summary>
    public static PriceHistoryResult Of(IReadOnlyList<DailyPrice> prices) => new(prices, Answered: true);

    /// <summary>El proveedor ha contestado que no tiene nada de este tramo.</summary>
    public static PriceHistoryResult Nothing { get; } = new([], Answered: true);

    /// <summary>El proveedor no ha podido contestar. El tramo sigue sin preguntar.</summary>
    public static PriceHistoryResult Failed { get; } = new([], Answered: false);
}

/// <summary>
/// Proveedor capaz de entregar el cierre diario de un activo entre dos fechas.
/// </summary>
/// <remarks>
/// Va aparte del precio de ahora porque la cobertura no coincide: hay proveedores que
/// dan el precio actual de un token pequeño y no su historia, y al revés. Un proveedor
/// que no cubra un activo contesta con una serie vacía, nunca con un error.
///
/// Caerse es otra cosa, y para eso está <see cref="PriceHistoryResult.Answered"/>: el
/// fallo se declara en el resultado y no propagando la excepción, porque una petición
/// que se cae no puede tumbar la pasada de los demás activos.
/// </remarks>
public interface IPriceHistoryProvider
{
    /// <summary>Nombre con el que queda anotado el origen de cada precio.</summary>
    string Name { get; }

    Task<PriceHistoryResult> GetHistoryAsync(
        PriceHistoryRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Un activo tal y como lo encuentra un proveedor al buscarlo.
/// </summary>
/// <param name="ProviderId">Con qué identificador lo conoce quien lo encontró.</param>
/// <param name="Market">
/// Dónde cotiza, cuando eso lo distinga de otro con el mismo símbolo. Vacío en lo que
/// no tiene mercado, como una criptomoneda.
/// </param>
public sealed record AssetSearchResult(
    string Symbol,
    string Name,
    Kapea.Domain.Assets.AssetClass Class,
    string ProviderId,
    string Provider,
    string? Market = null);

/// <summary>
/// Lo que devuelve una búsqueda, diciendo si pudo preguntarse a todos.
/// </summary>
/// <param name="IsComplete">
/// Falso cuando algún proveedor no respondió. Los resultados que hay siguen valiendo,
/// pero pueden faltar.
/// </param>
public sealed record AssetSearch(IReadOnlyList<AssetSearchResult> Results, bool IsComplete)
{
    public static AssetSearch Empty { get; } = new([], IsComplete: true);
}

/// <summary>
/// Proveedor capaz de buscar activos por nombre o por símbolo.
/// </summary>
/// <remarks>
/// Va aparte de dar precios porque no todo proveedor de precios sabe buscar, y porque
/// buscar es lo único que ocurre mientras el usuario espera mirando la pantalla.
/// </remarks>
public interface IAssetSearchProvider
{
    /// <summary>Nombre con el que se dice de dónde salió cada resultado.</summary>
    string Name { get; }

    Task<IReadOnlyList<AssetSearchResult>> SearchAsync(
        string text,
        CancellationToken cancellationToken = default);
}
