using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Abstractions;

/// <summary>
/// Resuelve el tipo aplicable a una operación. Solo se consulta al importar: una vez
/// congelado en el movimiento, ningún recálculo vuelve a pasar por aquí.
/// </summary>
public interface IExchangeRateProvider
{
    Task<ExchangeRate?> ResolveAsync(Currency currency, DateOnly date, CancellationToken cancellationToken = default);
}

/// <summary>Fuente externa de tipos publicados. La implementación de referencia es el BCE.</summary>
public interface IExchangeRateSource
{
    string Name { get; }

    Task<IReadOnlyList<DailyRate>> FetchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Almacén local de tipos publicados. Se sirve siempre desde aquí para que la red no
/// intervenga en un cálculo: un tipo que no se puede recuperar es un cálculo que no
/// se puede reproducir.
/// </summary>
public interface IExchangeRateStore
{
    /// <summary>Tipos de la divisa publicados en la fecha indicada o antes, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<DailyRate>> GetOnOrBeforeAsync(
        Currency currency,
        DateOnly date,
        int maximumLookbackDays,
        CancellationToken cancellationToken = default);

    /// <summary>Inserta o actualiza tipos. Reingestar el mismo rango no crea duplicados.</summary>
    Task<int> UpsertAsync(IReadOnlyList<DailyRate> rates, CancellationToken cancellationToken = default);
}

/// <summary>Precio de mercado de un activo en euros, con el instante al que corresponde.</summary>
public sealed record MarketPrice(string CanonicalSymbol, decimal PriceInEuros, DateTimeOffset AsOf);

/// <summary>
/// Precios de mercado para valorar las posiciones abiertas.
/// </summary>
/// <remarks>
/// Es un puerto y no una dependencia directa porque la elección del proveedor sigue
/// abierta para renta variable. Las posiciones están especificadas para funcionar sin
/// precio, así que un proveedor que no cubra un activo no rompe nada: la posición
/// aparece con cantidad y coste medio, y sin valor actual.
/// </remarks>
/// <summary>
/// Un activo del que se quiere el precio, tal y como hay que pedírselo al proveedor.
/// </summary>
/// <remarks>
/// No basta con el símbolo. Varios activos pueden compartirlo, así que hay proveedores
/// que sólo los distinguen por un identificador propio; pedirlos por símbolo obliga a
/// mantener a mano una lista de traducciones, y lo que no esté en ella se queda sin
/// precio para siempre. El identificador viaja con la petición, igual que ya viaja en la
/// del histórico.
/// </remarks>
/// <param name="CanonicalSymbol">Símbolo del catálogo, y clave del resultado.</param>
/// <param name="ProviderId">
/// Identificador con el que el proveedor conoce este activo, cuando se sabe. Manda sobre
/// cualquier traducción del símbolo. Vacío en lo que entró importando movimientos.
/// </param>
public sealed record QuotedAsset(string CanonicalSymbol, string? ProviderId = null);

public interface IMarketPriceProvider
{
    /// <summary>Precios de los activos que este proveedor cubra. Los que no cubra, simplemente no vienen.</summary>
    Task<IReadOnlyDictionary<string, MarketPrice>> GetPricesAsync(
        IReadOnlyCollection<QuotedAsset> assets,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Almacena la proyección de un activo: sus lotes, sus resultados y sus rendimientos.
/// Se reemplaza entera en cada recálculo, porque la fuente de verdad son los
/// movimientos y estas tablas son un derivado.
/// </summary>
public interface IPortfolioProjectionStore
{
    /// <summary>Activos que tienen algo proyectado: lotes, resultados o rendimientos.</summary>
    Task<IReadOnlyCollection<Guid>> ListProjectedAssetsAsync(CancellationToken cancellationToken = default);

    Task ReplaceAsync(
        Guid assetId,
        Domain.Calculation.AssetCalculationResult result,
        CancellationToken cancellationToken = default);
}
