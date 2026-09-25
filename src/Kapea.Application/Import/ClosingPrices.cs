using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Import;

/// <summary>
/// Da el precio de cierre en euros de un activo en un día.
/// </summary>
/// <remarks>
/// Existe porque hay orígenes que entregan un movimiento sin valorar —el libro de
/// Kraken no pone ningún euro a un cambio de una cripto por otra— y el cálculo no
/// puede quedarse sin esa cifra sin descuadrar la cartera entera.
/// </remarks>
public interface IClosingPrices
{
    /// <summary>El cierre de ese día, o nada si no hay forma de saberlo.</summary>
    Task<decimal?> FindAsync(Asset asset, DateOnly day, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resuelve el cierre del histórico ya guardado y, sólo si falta, lo pide al proveedor.
/// </summary>
/// <remarks>
/// El orden importa por coste y por reproducibilidad: el histórico es el mismo dato que
/// alimenta las gráficas, así que valorar con él da la misma cifra que se ve en
/// pantalla, y una importación de cien permutas no dispara cien llamadas externas.
///
/// Lo que se descarga se guarda, de modo que la siguiente valoración del mismo día ya
/// no sale de la red.
/// </remarks>
public sealed class ClosingPrices(
    IPriceHistoryStore store,
    IPriceHistoryProvider provider,
    ILogger<ClosingPrices> logger) : IClosingPrices
{
    public async Task<decimal?> FindAsync(Asset asset, DateOnly day, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (await StoredAsync(asset.Id, day, cancellationToken).ConfigureAwait(false) is { } stored)
        {
            return stored;
        }

        var downloaded = await provider
            .GetHistoryAsync(
                    new PriceHistoryRequest(asset.Id, asset.CanonicalSymbol, asset.Class, day, day, asset.ProviderId),
                    cancellationToken)
            .ConfigureAwait(false);

        if (downloaded.Count == 0)
        {
            logger.LogWarning(
                "Sin precio de cierre para {Activo} el {Dia}: el movimiento queda sin valorar.", asset.CanonicalSymbol, day);

            return null;
        }

        await store.UpsertAsync(downloaded, cancellationToken).ConfigureAwait(false);

        return downloaded.FirstOrDefault(price => price.Date == day)?.PriceInEuros;
    }

    private async Task<decimal?> StoredAsync(Guid assetId, DateOnly day, CancellationToken cancellationToken)
    {
        var prices = await store.GetAsync(assetId, day, day, cancellationToken).ConfigureAwait(false);

        return prices.FirstOrDefault(price => price.Date == day)?.PriceInEuros;
    }
}
