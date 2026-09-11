using Kapea.Application.Abstractions;
using Kapea.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Pide la serie a los proveedores por orden hasta completar el rango.
/// </summary>
/// <remarks>
/// El orden importa y es el contrario al del precio de ahora: Yahoo va primero porque
/// entrega años de historia, y CoinGecko después para los días que Yahoo no cubra,
/// dentro de su ventana gratuita de un año. Preguntar al segundo solo por lo que falta
/// evita gastar cuota en lo que ya se tiene.
///
/// Un activo que ninguno cubra devuelve serie vacía. No es un fallo: la posición se
/// mostrará sin valor esos días, que es como está hoy.
/// </remarks>
public sealed class PriceHistoryDispatcher(
    IEnumerable<IPriceHistoryProvider> providers,
    ILogger<PriceHistoryDispatcher> logger) : IPriceHistoryProvider
{
    public string Name => "Kapea";

    public async Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
        PriceHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var byDay = new Dictionary<DateOnly, DailyPrice>();

        foreach (var provider in providers)
        {
            var missing = Missing(request, byDay);

            if (missing is null)
            {
                break;
            }

            var prices = await provider.GetHistoryAsync(missing, cancellationToken).ConfigureAwait(false);

            foreach (var price in prices)
            {
                // El primero que dé un día se queda con él: el orden de los proveedores
                // es la preferencia, y reescribir haría que el pasado cambiara de valor.
                byDay.TryAdd(price.Date, price);
            }
        }

        if (byDay.Count == 0)
        {
            logger.LogInformation(
                "Ningún proveedor cubre {Simbolo} entre {Desde} y {Hasta}.",
                request.CanonicalSymbol, request.From, request.To);
        }

        return [.. byDay.Values.OrderBy(price => price.Date)];
    }

    /// <summary>
    /// El tramo que aún falta, o null si ya está todo.
    /// </summary>
    /// <remarks>
    /// Se acota al primer y último día sin precio. Afinar más, hueco a hueco,
    /// multiplicaría las peticiones para ahorrar días que el proveedor devuelve igual
    /// dentro del mismo rango.
    /// </remarks>
    private static PriceHistoryRequest? Missing(
        PriceHistoryRequest request,
        Dictionary<DateOnly, DailyPrice> found)
    {
        DateOnly? first = null;
        DateOnly? last = null;

        for (var day = request.From; day <= request.To; day = day.AddDays(1))
        {
            if (found.ContainsKey(day))
            {
                continue;
            }

            first ??= day;
            last = day;
        }

        return first is { } from && last is { } to ? request with { From = from, To = to } : null;
    }
}
