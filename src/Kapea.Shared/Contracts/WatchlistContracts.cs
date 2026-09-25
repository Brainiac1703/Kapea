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
/// <param name="HasPrices">
/// Algún proveedor da precios de este activo. Cuando es falso el activo se añade
/// igualmente, pero no tendrá precio ni señales hasta que los haya.
/// </param>
public sealed record WatchAssetResponse(
    WatchedAssetResponse Asset,
    bool AlreadyWatched,
    bool HasPrices);
