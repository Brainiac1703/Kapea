using Kapea.Application.Abstractions;
using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.ExchangeRates;

/// <summary>
/// Sirve los tipos desde el almacén local. La red no interviene: un cálculo que
/// dependiera de que una web responda no sería reproducible.
/// </summary>
public sealed class StoredExchangeRateProvider(IExchangeRateStore store) : IExchangeRateProvider
{
    /// <summary>
    /// Margen de búsqueda hacia atrás cuando el día pedido no tiene publicación. Diez
    /// días cubren cualquier puente del calendario del BCE; más allá, el hueco no es
    /// un festivo sino un fallo de ingesta que conviene que salte.
    /// </summary>
    internal const int MaximumLookbackDays = 10;

    public async Task<ExchangeRate?> ResolveAsync(
        Currency currency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (currency.IsEuro)
        {
            return null;
        }

        var published = await store
            .GetOnOrBeforeAsync(currency, date, MaximumLookbackDays, cancellationToken)
            .ConfigureAwait(false);

        return ExchangeRateResolution.Resolve(currency, date, published);
    }
}
