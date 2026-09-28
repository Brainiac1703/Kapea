using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Domain.Common;
using Kapea.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Watchlist;

/// <summary>Lo que el seguimiento necesita leer y escribir.</summary>
public interface IWatchlistRepository
{
    /// <summary>
    /// Lo que el usuario vigila: lo que ha añadido y lo que tiene, sin repetir.
    /// </summary>
    /// <remarks>
    /// Tener posición cuenta como vigilarlo, se consulte como se consulte. Así nadie
    /// tiene que acordarse de añadir al comprar, y un activo con posición no puede
    /// quedarse fuera aunque le falte su fila.
    /// </remarks>
    Task<IReadOnlyList<WatchedAssetResponse>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>El activo del catálogo con ese símbolo y esa clase, si existe.</summary>
    Task<Asset?> FindAsync(string symbol, AssetClass assetClass, CancellationToken cancellationToken = default);

    Task<Asset> AddToCatalogueAsync(
        string symbol,
        AssetClass assetClass,
        CancellationToken cancellationToken = default,
        string? displayName = null,
        string? providerId = null);

    /// <summary>
    /// El activo del catálogo que corresponde a un resultado de búsqueda, si lo hay.
    /// </summary>
    /// <remarks>
    /// El símbolo del proveedor no tiene por qué ser el del catálogo: Kapea guarda los
    /// valores como los nombra el bróker —NOW.US— y Yahoo los nombra sin el mercado
    /// —NOW—. Reconocerlo es lo que evita crear un segundo activo con su propia cola de
    /// lotes para lo mismo.
    /// </remarks>
    Task<Asset?> FindByProviderAsync(
        AssetSearchResult result,
        CancellationToken cancellationToken = default);

    Task<bool> IsWatchedAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task WatchAsync(Guid assetId, DateTimeOffset addedAt, CancellationToken cancellationToken = default);

    Task StopWatchingAsync(Guid assetId, CancellationToken cancellationToken = default);

    /// <summary>Queda posición abierta de ese activo.</summary>
    Task<bool> IsHeldAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task<WatchedAssetResponse?> FindWatchedAsync(Guid assetId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Se ha pedido dejar de seguir un activo que todavía se tiene.</summary>
public sealed class HeldAssetException(string message) : DomainException(message);

/// <summary>
/// Qué precios tiene un activo de los dos que el usuario va a ver.
/// </summary>
/// <remarks>
/// Dos y no uno porque son proveedores distintos y su cobertura no coincide. Un sí o no
/// podía acertar y engañar a la vez: un activo con años de histórico y sin cotización
/// pasaba como cubierto y aparecía en la lista con la columna de precio vacía.
///
/// El remedio tampoco es el mismo. Sin cotización suele bastar con volver a añadirlo
/// eligiéndolo de una búsqueda, que es lo que le da el identificador con el que su
/// proveedor lo conoce; sin serie no hay nada que hacer desde la aplicación.
/// </remarks>
/// <param name="HasSeries">Hay histórico: habrá gráfica e indicadores.</param>
/// <param name="HasQuote">Hay precio de ahora: aparecerá en la columna de precio.</param>
public sealed record PriceCoverage(bool HasSeries, bool HasQuote)
{
    /// <summary>Tiene las dos, que es lo único que se ve completo en pantalla.</summary>
    public bool IsComplete => HasSeries && HasQuote;
}

/// <summary>
/// Gestiona qué activos vigila el usuario.
/// </summary>
/// <remarks>
/// Existe para que un sistema de entrada sirva para lo que existe: decidir dónde
/// entrar. Mientras el catálogo sólo se llenara importando movimientos, Kapea únicamente
/// podía señalar lo que ya se había comprado.
/// </remarks>
public sealed class WatchlistService(
    IWatchlistRepository repository,
    IPriceHistoryProvider prices,
    IMarketPriceProvider quotes,
    TimeProvider timeProvider,
    ILogger<WatchlistService> logger)
{
    /// <summary>Cuántos días atrás se mira para saber si un proveedor cubre el activo.</summary>
    private const int CoverageDays = 10;

    public Task<IReadOnlyList<WatchedAssetResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(cancellationToken);

    public async Task<WatchAssetResponse> AddAsync(
        string symbol,
        AssetClass assetClass,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var asset = await repository.FindAsync(symbol, assetClass, cancellationToken).ConfigureAwait(false)
            ?? await repository.AddToCatalogueAsync(symbol, assetClass, cancellationToken).ConfigureAwait(false);

        var already = await repository.IsWatchedAsync(asset.Id, cancellationToken).ConfigureAwait(false);

        if (!already)
        {
            await repository
                .WatchAsync(asset.Id, timeProvider.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var covered = await CoverageAsync(asset, cancellationToken).ConfigureAwait(false);

        if (!covered.IsComplete)
        {
            logger.LogWarning(
                "Cobertura incompleta de {Activo}: histórico {Serie}, cotización {Cotizacion}.",
                asset.CanonicalSymbol, covered.HasSeries, covered.HasQuote);
        }

        var watched = await repository.FindWatchedAsync(asset.Id, cancellationToken).ConfigureAwait(false);

        return new WatchAssetResponse(watched!, already, covered.HasSeries, covered.HasQuote);
    }

    /// <summary>Empieza a seguir el activo que el usuario ha elegido de una búsqueda.</summary>
    public async Task<WatchAssetResponse> AddAsync(
        AssetSearchResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var asset = await repository.FindByProviderAsync(result, cancellationToken).ConfigureAwait(false);

        if (asset is null)
        {
            asset = await repository
                .AddToCatalogueAsync(result.Symbol, result.Class, cancellationToken, result.Name, result.ProviderId)
                .ConfigureAwait(false);
        }
        else
        {
            // Ya existía, quizá con otro símbolo: se queda el suyo, que es con el que
            // están sus movimientos, y aprende con qué identificador pedir sus precios.
            asset.KnownAs(result.ProviderId, result.Name);
        }

        var already = await repository.IsWatchedAsync(asset.Id, cancellationToken).ConfigureAwait(false);

        if (!already)
        {
            await repository.WatchAsync(asset.Id, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var covered = await CoverageAsync(asset, cancellationToken).ConfigureAwait(false);
        var watched = await repository.FindWatchedAsync(asset.Id, cancellationToken).ConfigureAwait(false);

        return new WatchAssetResponse(watched!, already, covered.HasSeries, covered.HasQuote);
    }

    public async Task RemoveAsync(Guid assetId, CancellationToken cancellationToken = default)
    {
        // Un activo que se tiene y no se vigila dejaría una posición sin precio, sin
        // señales y sin evolución. Es peor que no poder quitarlo.
        if (await repository.IsHeldAsync(assetId, cancellationToken).ConfigureAwait(false))
        {
            throw new HeldAssetException(
                "Este activo está en tu cartera, así que se sigue mientras lo tengas. Podrás dejar de seguirlo cuando no te quede posición.");
        }

        await repository.StopWatchingAsync(assetId, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pregunta al proveedor si tiene precios recientes del activo.
    /// </summary>
    /// <remarks>
    /// No bloquea el alta: un proveedor puede fallar o tardar en cubrir algo nuevo, y lo
    /// que hoy no tiene precio puede tenerlo mañana. Lo que no puede pasar es que el
    /// usuario lo añada y lo descubra tres días después.
    /// </remarks>
    private async Task<PriceCoverage> CoverageAsync(Asset asset, CancellationToken cancellationToken)
    {
        var series = await HasSeriesAsync(asset, cancellationToken).ConfigureAwait(false);
        var quote = await HasQuoteAsync(asset, cancellationToken).ConfigureAwait(false);

        return new PriceCoverage(series, quote);
    }

    /// <summary>Si algún proveedor da el histórico reciente del activo.</summary>
    private async Task<bool> HasSeriesAsync(Asset asset, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            var series = await prices
                .GetHistoryAsync(
                    new PriceHistoryRequest(
                        asset.Id, asset.CanonicalSymbol, asset.Class, today.AddDays(-CoverageDays), today, asset.ProviderId),
                    cancellationToken)
                .ConfigureAwait(false);

            // Un proveedor caído no es lo mismo que un activo sin cobertura, y el
            // resultado ya los distingue, pero aquí se responde igual a las dos: lo que
            // el usuario ve en ese momento es que no hay precios. La distinción importa
            // donde decide si un tramo hay que volver a pedirlo, no en un aviso.
            return series.Prices.Count > 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Queda el fallo que ningún proveedor llegue a declarar, por ejemplo uno que
            // lance antes de responder. Se dice que no hay precios y se deja constancia.
            logger.LogWarning(exception, "No se ha podido comprobar el histórico de {Activo}.", asset.CanonicalSymbol);

            return false;
        }
    }

    /// <summary>
    /// Si algún proveedor da el precio de ahora del activo.
    /// </summary>
    /// <remarks>
    /// Es la pregunta que faltaba. La columna de precio de la lista no sale del
    /// histórico sino de aquí, y la cobertura de los dos no coincide: hay activos con
    /// años de serie a los que nadie cotiza, y preguntar sólo por la serie los daba por
    /// buenos.
    /// </remarks>
    private async Task<bool> HasQuoteAsync(Asset asset, CancellationToken cancellationToken)
    {
        try
        {
            var quoted = await quotes
                .GetPricesAsync([new QuotedAsset(asset.CanonicalSymbol, asset.ProviderId)], cancellationToken)
                .ConfigureAwait(false);

            return quoted.Count > 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "No se ha podido comprobar la cotización de {Activo}.", asset.CanonicalSymbol);

            return false;
        }
    }

}
