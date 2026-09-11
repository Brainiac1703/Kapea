using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.MarketData;

/// <summary>
/// Se asegura de que los tipos publicados de un rango estén guardados.
/// </summary>
/// <remarks>
/// Hasta ahora no hacía falta: todos los movimientos venían en euros. Lo pide el
/// histórico de precios, porque la única fuente que llega más atrás de un año cotiza
/// algunos activos en dólares y hay que pasarlos a euros con el tipo del día.
///
/// Se comprueba antes de descargar: el fichero histórico del BCE son décadas enteras, y
/// bajarlo en cada vuelta del trabajador sería gastar por nada.
/// </remarks>
public sealed class ExchangeRateIngestion(
    IExchangeRateSource source,
    IExchangeRateStore store,
    ILogger<ExchangeRateIngestion> logger)
{
    public async Task<int> EnsureAsync(
        Currency currency,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (currency.IsEuro || from > to)
        {
            return 0;
        }

        // Basta con mirar si hay alguno en el rango: el BCE publica en bloque, así que o
        // está el tramo o no está. Comprobar día a día multiplicaría las consultas para
        // detectar los festivos, que no se van a poder rellenar de todos modos.
        var stored = await store
            .GetOnOrBeforeAsync(currency, to, to.DayNumber - from.DayNumber, cancellationToken)
            .ConfigureAwait(false);

        if (stored.Count > 0 && stored[^1].Date <= from.AddDays(7))
        {
            return 0;
        }

        var published = await source.FetchAsync(from, to, cancellationToken).ConfigureAwait(false);

        var written = await store
            .UpsertAsync([.. published.Where(rate => rate.Currency == currency)], cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Guardados {Tipos} tipos de {Divisa} entre {Desde} y {Hasta}.", written, currency.Code, from, to);

        return written;
    }
}
