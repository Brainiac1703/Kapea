using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
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
/// <param name="LastHeldOn">
/// Día en que se vendió del todo, o nada si sigue en cartera.
/// </param>
/// <remarks>
/// Un activo vendido entero sigue necesitando precios de cuando se tenía: sin ellos, la
/// gráfica de aquellos meses saldría corta sin que nada lo explicara. Lo que no necesita
/// es precio de hoy.
/// </remarks>
/// <param name="ProviderId">
/// Identificador con el que el proveedor conoce el activo, cuando se sabe. Vacío en lo
/// que entró importando movimientos, que se resuelve por su símbolo.
/// </param>
/// <param name="DesiredFrom">
/// Hasta dónde se quiere la serie, que es todo lo que el proveedor tenga.
/// </param>
/// <remarks>
/// Dos fechas y no una porque responden a cosas distintas. <see cref="FirstHeldOn"/> es
/// el mínimo que hay que cubrir para poder valorar lo que se tuvo y evaluar los sistemas
/// declarados; <see cref="DesiredFrom"/> es hasta dónde interesa llegar. Lo primero
/// corre prisa y lo segundo no, y por eso se piden en momentos distintos.
///
/// Sin <see cref="DesiredFrom"/>, no hay serie, se pide desde el mínimo, que es lo que
/// se hacía antes de querer más historia.
/// </remarks>
public sealed record PricedAsset(
    Guid AssetId,
    string CanonicalSymbol,
    AssetClass Class,
    DateOnly FirstHeldOn,
    DateOnly? LastHeldOn = null,
    string? ProviderId = null,
    DateOnly? DesiredFrom = null)
{
    /// <summary>El día más antiguo que se llegará a pedir, cubriendo siempre el mínimo.</summary>
    public DateOnly Earliest =>
        DesiredFrom is { } wanted && wanted < FirstHeldOn ? wanted : FirstHeldOn;
}

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

    /// <summary>Un tramo por pedir, y si es de los que corren prisa o de los que no.</summary>
    /// <param name="IsBackfill">Historia antigua: interesa, pero puede esperar.</param>
    private readonly record struct Gap(DateOnly From, DateOnly To, bool IsBackfill);

    /// <summary>
    /// Los tramos de días que todavía hay que pedir.
    /// </summary>
    /// <remarks>
    /// Hacia atrás manda lo pedido y no lo guardado. Un activo que empezó a cotizar
    /// después del suelo devuelve su primera cotización y nada antes; si el tramo
    /// anterior se decidiera por lo guardado, seguiría pareciendo un hueco y volvería a
    /// pedirse en cada vuelta, para siempre y sin que nada lo delatara.
    ///
    /// Hacia delante manda lo guardado, porque el cierre de hoy puede no existir todavía
    /// cuando se pregunta: darlo por pedido dejaría la serie un día corta hasta mañana.
    /// </remarks>
    private static IEnumerable<Gap> Missing(
        PricedAsset asset,
        StoredRange? covered,
        PriceHistoryReach? reach,
        DateOnly until)
    {
        // Lo pedido con otro identificador no dice nada de lo que ahora se pide: se
        // estaba preguntando por otro activo del proveedor.
        var asked = reach is not null && reach.AnswersFor(asset.ProviderId) ? reach : null;
        var lastKnown = covered?.Last ?? asked?.RequestedTo;
        var earliestAsked = asked?.RequestedFrom ?? covered?.First;

        if (lastKnown is null)
        {
            // Nada todavía. El mínimo va primero y entero, para que el activo sirva ya.
            if (asset.FirstHeldOn <= until)
            {
                yield return new Gap(asset.FirstHeldOn, until, IsBackfill: false);
            }

            if (asset.Earliest < asset.FirstHeldOn)
            {
                yield return new Gap(asset.Earliest, asset.FirstHeldOn.AddDays(-1), IsBackfill: true);
            }

            yield break;
        }

        if (earliestAsked is { } first)
        {
            // Lo que falte del mínimo sigue corriendo prisa; lo de más atrás, no.
            var covers = first < asset.FirstHeldOn ? first : asset.FirstHeldOn;

            if (asset.FirstHeldOn < first)
            {
                yield return new Gap(asset.FirstHeldOn, first.AddDays(-1), IsBackfill: false);
            }

            if (asset.Earliest < covers)
            {
                yield return new Gap(asset.Earliest, covers.AddDays(-1), IsBackfill: true);
            }
        }

        if (lastKnown.Value < until)
        {
            yield return new Gap(lastKnown.Value.AddDays(1), until, IsBackfill: false);
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

        // Desde el suelo y no desde el mínimo: sin el tipo del día, un precio en dólares
        // no se puede convertir y ese día se queda fuera de la serie. Pedir los tipos más
        // tarde que los precios cortaría la historia de todo lo que cotiza en dólares sin
        // que el proveedor de precios tuviera nada que ver.
        await exchangeRates
            .EnsureAsync(Dollar, priced.Min(asset => asset.Earliest), today, cancellationToken)
            .ConfigureAwait(false);

        var ids = priced.Select(asset => asset.AssetId).ToList();
        var stored = new Dictionary<Guid, StoredRange>(
            await store.GetStoredRangeAsync(ids, cancellationToken).ConfigureAwait(false));
        var reached = new Dictionary<Guid, PriceHistoryReach>(
            await store.GetReachAsync(ids, cancellationToken).ConfigureAwait(false));

        var written = 0;
        var backfilled = 0;

        // Dos vueltas y no una: primero se pone al día todo y después se rellena hacia
        // atrás. Hacer cada activo entero dejaría al último sin precio de hoy hasta
        // terminar los anteriores, y sin ninguno si la cuota se agota antes. Lo urgente
        // cabe siempre, y lo que se queda a medias es lo que puede esperar.
        foreach (var backfilling in new[] { false, true })
        {
            foreach (var asset in priced)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var until = asset.LastHeldOn is { } sold && sold < today ? sold : today;
                var gaps = Missing(asset, stored.GetValueOrDefault(asset.AssetId), reached.GetValueOrDefault(asset.AssetId), until)
                    .Where(gap => gap.IsBackfill == backfilling)
                    .ToList();

                foreach (var gap in gaps)
                {
                    var prices = await provider
                        .GetHistoryAsync(
                            new PriceHistoryRequest(
                                asset.AssetId, asset.CanonicalSymbol, asset.Class, gap.From, gap.To, asset.ProviderId),
                            cancellationToken)
                        .ConfigureAwait(false);

                    var savedDays = await store.UpsertAsync([.. prices], cancellationToken).ConfigureAwait(false);
                    written += savedDays;

                    if (backfilling)
                    {
                        backfilled += savedDays;

                        // La primera pasada dura horas y no se ve desde ninguna pantalla:
                        // sin esto, saber por dónde va exige mirar la base de datos.
                        logger.LogInformation(
                            "Relleno de {Activo}: {Dias} días entre {Desde} y {Hasta}.",
                            asset.CanonicalSymbol, savedDays, gap.From, gap.To);
                    }

                    // Se deja constancia aunque no haya venido nada: ese es justamente el
                    // tramo que no hay que volver a pedir.
                    var asked = reached.TryGetValue(asset.AssetId, out var previous)
                        && previous.AnswersFor(asset.ProviderId)
                        ? previous.Including(gap.From, gap.To)
                        : PriceHistoryReach.Of(asset.AssetId, gap.From, gap.To, asset.ProviderId);

                    await store.RecordReachAsync(asked, cancellationToken).ConfigureAwait(false);

                    reached[asset.AssetId] = asked;

                    if (prices.Count > 0)
                    {
                        var first = prices.Min(price => price.Date);
                        var last = prices.Max(price => price.Date);

                        stored[asset.AssetId] = stored.TryGetValue(asset.AssetId, out var range)
                            ? new StoredRange(
                                first < range.First ? first : range.First,
                                last > range.Last ? last : range.Last)
                            : new StoredRange(first, last);
                    }
                }
            }
        }

        // Sin cobertura es no tener ni un día, no que falte historia antigua: un activo
        // que empezó a cotizar después del suelo tiene toda la serie que puede tener.
        var uncovered = priced.Count(asset => !stored.ContainsKey(asset.AssetId));

        var pending = priced.Count(asset =>
            Missing(
                asset,
                stored.GetValueOrDefault(asset.AssetId),
                reached.GetValueOrDefault(asset.AssetId),
                asset.LastHeldOn is { } sold && sold < today ? sold : today)
            .Any(gap => gap.IsBackfill));

        logger.LogInformation(
            "Histórico de precios al día: {Dias} días nuevos de {Activos} activos, "
            + "{Relleno} de historia antigua, {Pendientes} activos con relleno pendiente, {Sin} sin cobertura.",
            written, priced.Count, backfilled, pending, uncovered);

        return new PriceHistoryUpdate(priced.Count, written, uncovered);
    }
}
