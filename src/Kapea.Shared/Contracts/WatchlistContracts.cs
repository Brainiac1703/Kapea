namespace Kapea.Shared.Contracts;

/// <summary>
/// Un activo que el usuario vigila, con la situación en la que está.
/// </summary>
/// <param name="IsHeld">Tiene posición abierta. Lo que se tiene se vigila siempre.</param>
/// <param name="Quantity">Unidades en cartera. Nula cuando sólo se vigila.</param>
/// <param name="MarketValueInEuros">Lo que vale la posición hoy. Nula sin posición o sin precio.</param>
/// <param name="LastPriceInEuros">Último precio conocido. Nulo cuando no hay ninguno.</param>
/// <param name="PriceAsOf">A qué día corresponde ese precio.</param>
public sealed record WatchedAssetResponse(
    Guid AssetId,
    string Symbol,
    string DisplayName,
    string AssetClass,
    bool IsHeld,
    decimal? Quantity,
    decimal? MarketValueInEuros,
    decimal? LastPriceInEuros,
    DateOnly? PriceAsOf);

/// <summary>Lo que hace falta para empezar a vigilar un activo.</summary>
public sealed record WatchAssetRequest(string Symbol, string AssetClass);

/// <summary>
/// Resultado de añadir un activo al seguimiento.
/// </summary>
/// <param name="AlreadyWatched">Ya estaba en la lista; no se ha duplicado.</param>
/// <param name="HasSeries">
/// Algún proveedor da el histórico: habrá gráfica e indicadores.
/// </param>
/// <param name="HasQuote">
/// Algún proveedor da el precio de ahora: aparecerá en la columna de precio.
/// </param>
/// <remarks>
/// Dos y no uno porque son proveedores distintos y su cobertura no coincide. Un solo
/// indicador podía acertar y engañar a la vez: un activo con años de histórico y sin
/// cotización pasaba como cubierto y aparecía en la lista sin precio.
///
/// El activo se añade igualmente falte lo que falte; lo que cambia es de qué se avisa,
/// porque el remedio no es el mismo.
/// </remarks>
public sealed record WatchAssetResponse(
    WatchedAssetResponse Asset,
    bool AlreadyWatched,
    bool HasSeries,
    bool HasQuote);

/// <summary>Un activo encontrado al buscar, con lo necesario para reconocerlo y elegirlo.</summary>
public sealed record AssetSearchResultResponse(
    string Symbol,
    string Name,
    string AssetClass,
    string ProviderId,
    string Provider,
    string? Market);

/// <summary>
/// Lo que devuelve una búsqueda.
/// </summary>
/// <param name="IsComplete">Falso si algún proveedor no respondió: pueden faltar resultados.</param>
public sealed record AssetSearchResponse(
    IReadOnlyList<AssetSearchResultResponse> Results,
    bool IsComplete);

/// <summary>Seguir un activo elegido de una búsqueda.</summary>
public sealed record FollowAssetRequest(
    string Symbol,
    string Name,
    string AssetClass,
    string ProviderId,
    string Provider,
    string? Market);
