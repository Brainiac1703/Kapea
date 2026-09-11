namespace Kapea.Domain.MarketData;

/// <summary>
/// Precio de cierre de un activo en un día, en euros.
/// </summary>
/// <remarks>
/// Es un catálogo global y no un dato de cada usuario: lo que valió bitcoin el martes
/// es lo mismo para todos, y guardarlo por duplicado multiplicaría las peticiones a los
/// proveedores sin añadir nada.
///
/// El origen viaja con el precio porque no todas las fuentes cubren los mismos activos
/// ni el mismo alcance: saber de dónde salió cada cifra permite volver a pedir solo lo
/// que trajo una fuente concreta si resulta estar mal.
/// </remarks>
/// <param name="AssetId">Activo del catálogo al que corresponde.</param>
/// <param name="Date">Día del cierre.</param>
/// <param name="PriceInEuros">Precio de cierre en euros.</param>
/// <param name="Source">Proveedor del que se obtuvo.</param>
public sealed record DailyPrice(Guid AssetId, DateOnly Date, decimal PriceInEuros, string Source);
