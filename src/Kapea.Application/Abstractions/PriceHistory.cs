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
    /// Hasta qué día llega la serie de cada activo, o su ausencia si no tiene ninguno.
    /// </summary>
    /// <remarks>
    /// Es lo que permite pedir al proveedor solo lo que falta en lugar del histórico
    /// entero cada vez.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, DateOnly>> GetLastStoredDayAsync(
        IReadOnlyCollection<Guid> assetIds,
        CancellationToken cancellationToken = default);

    /// <summary>Guarda una serie descargada. Un día ya guardado no se duplica.</summary>
    Task<int> UpsertAsync(IReadOnlyList<DailyPrice> prices, CancellationToken cancellationToken = default);
}

/// <summary>Lo que hace falta para pedir la serie de un activo a un proveedor.</summary>
/// <param name="AssetId">Activo del catálogo, para devolver la serie ya atribuida.</param>
/// <param name="CanonicalSymbol">Símbolo del catálogo, que cada proveedor traduce al suyo.</param>
/// <param name="Class">Clase del activo: decide cómo se nombra en el proveedor.</param>
public sealed record PriceHistoryRequest(
    Guid AssetId,
    string CanonicalSymbol,
    Kapea.Domain.Assets.AssetClass Class,
    DateOnly From,
    DateOnly To);

/// <summary>
/// Proveedor capaz de entregar el cierre diario de un activo entre dos fechas.
/// </summary>
/// <remarks>
/// Va aparte del precio de ahora porque la cobertura no coincide: hay proveedores que
/// dan el precio actual de un token pequeño y no su historia, y al revés. Un proveedor
/// que no cubra un activo devuelve una serie vacía, nunca un error.
/// </remarks>
public interface IPriceHistoryProvider
{
    /// <summary>Nombre con el que queda anotado el origen de cada precio.</summary>
    string Name { get; }

    Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
        PriceHistoryRequest request,
        CancellationToken cancellationToken = default);
}
