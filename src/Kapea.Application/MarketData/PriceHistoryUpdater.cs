using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.MarketData;

/// <summary>Lo que el relleno necesita saber de los activos con posición.</summary>
public interface IPricedAssetRepository
{
    /// <summary>
    /// Activos con posición abierta y el día de su primera adquisición.
    /// </summary>
    /// <remarks>
    /// Recorre todos los usuarios: los precios son un catálogo global y no tiene sentido
    /// descargar dos veces lo que valió bitcoin el martes.
    /// </remarks>
    Task<IReadOnlyList<PricedAsset>> ListAsync(CancellationToken cancellationToken = default);
}

/// <param name="FirstHeldOn">Día de la primera adquisición: antes de él no hay nada que valorar.</param>
public sealed record PricedAsset(Guid AssetId, string CanonicalSymbol, AssetClass Class, DateOnly FirstHeldOn);

/// <summary>Lo que dejó una pasada del relleno.</summary>
public sealed record PriceHistoryUpdate(int Assets, int DaysWritten, int AssetsWithoutCoverage);

/// <summary>
/// Completa la serie de precios de cada activo con posición.
/// </summary>
/// <remarks>
/// Pide solo lo que falta: desde el día siguiente al último guardado, o desde la primera
/// adquisición si no hay nada, hasta hoy. Una segunda pasada el mismo día no pide nada,
/// que es lo que permite ejecutarlo cada pocas horas sin agotar las cuotas gratuitas.
///
/// Lo descargado se guarda activo a activo. Si un proveedor falla a mitad, lo anterior ya
/// está guardado y lo que falte se vuelve a pedir en la siguiente vuelta.
/// </remarks>
public sealed class PriceHistoryUpdater(
    IPricedAssetRepository assets,
    IPriceHistoryStore store,
    IPriceHistoryProvider provider,
    ExchangeRateIngestion exchangeRates,
    TimeProvider timeProvider,
    ILogger<PriceHistoryUpdater> logger)
{
    /// <summary>
    /// Divisa en la que cotizan los activos que ninguna fuente da en euros.
    /// </summary>
    /// <remarks>
    /// Yahoo es la única fuente que llega más atrás de un año, y a los tokens pequeños
    /// solo los cotiza contra el dólar. Sus tipos tienen que estar guardados antes de
    /// convertir, o esos días se quedarían fuera de la serie.
    /// </remarks>
    private static readonly Currency Dollar = Currency.FromCode("USD");

    /// <summary>Los tramos de días que la serie todavía no cubre.</summary>
    private static IEnumerable<(DateOnly From, DateOnly To)> Missing(
        PricedAsset asset,
        StoredRange? covered,
        DateOnly today)
    {
        if (covered is null)
        {
            if (asset.FirstHeldOn <= today)
            {
                yield return (asset.FirstHeldOn, today);
            }

            yield break;
        }

        if (asset.FirstHeldOn < covered.First)
        {
            yield return (asset.FirstHeldOn, covered.First.AddDays(-1));
        }

        if (covered.Last < today)
        {
            yield return (covered.Last.AddDays(1), today);
        }
    }

    public async Task<PriceHistoryUpdate> UpdateAsync(CancellationToken cancellationToken = default)
    {
        var priced = await assets.ListAsync(cancellationToken).ConfigureAwait(false);

        if (priced.Count == 0)
        {
            return new PriceHistoryUpdate(0, 0, 0);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        await exchangeRates
            .EnsureAsync(Dollar, priced.Min(asset => asset.FirstHeldOn), today, cancellationToken)
            .ConfigureAwait(false);

        var stored = await store
            .GetStoredRangeAsync([.. priced.Select(asset => asset.AssetId)], cancellationToken)
            .ConfigureAwait(false);

        var written = 0;
        var uncovered = 0;

        foreach (var asset in priced)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var covered = stored.GetValueOrDefault(asset.AssetId);
            var downloaded = 0;

            // Dos tramos y no uno: lo que falta al final, que es lo habitual, y lo que
            // falte al principio, que aparece cuando la serie se descargó con un
            // proveedor que entonces no llegaba tan atrás.
            foreach (var (from, to) in Missing(asset, covered, today))
            {
                var prices = await provider
                    .GetHistoryAsync(
                        new PriceHistoryRequest(asset.AssetId, asset.CanonicalSymbol, asset.Class, from, to),
                        cancellationToken)
                    .ConfigureAwait(false);

                downloaded += prices.Count;
                written += await store.UpsertAsync([.. prices], cancellationToken).ConfigureAwait(false);
            }

            // Sin cobertura es no tener ni un día, no que hoy todavía no haya cerrado:
            // contar lo segundo haría parecer rota una serie que está al día.
            if (downloaded == 0 && covered is null)
            {
                uncovered++;
            }
        }

        logger.LogInformation(
            "Histórico de precios al día: {Dias} días nuevos de {Activos} activos, {Sin} sin cobertura.",
            written, priced.Count, uncovered);

        return new PriceHistoryUpdate(priced.Count, written, uncovered);
    }
}
