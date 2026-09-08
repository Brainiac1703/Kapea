using Kapea.Application.Abstractions;
using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// Almacén local de tipos publicados. La clave es divisa y fecha, así que reingestar
/// el mismo rango actualiza en lugar de duplicar.
/// </summary>
public sealed class ExchangeRateStore(KapeaDbContext context) : IExchangeRateStore
{
    public async Task<IReadOnlyList<DailyRate>> GetOnOrBeforeAsync(
        Currency currency,
        DateOnly date,
        int maximumLookbackDays,
        CancellationToken cancellationToken = default)
    {
        var earliest = date.AddDays(-Math.Abs(maximumLookbackDays));

        return await context.DailyRates
            .Where(rate => rate.Currency == currency && rate.Date <= date && rate.Date >= earliest)
            .OrderByDescending(rate => rate.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> UpsertAsync(
        IReadOnlyList<DailyRate> rates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rates);

        if (rates.Count == 0)
        {
            return 0;
        }

        var currencies = rates.Select(rate => rate.Currency).Distinct().ToList();
        var earliest = rates.Min(rate => rate.Date);
        var latest = rates.Max(rate => rate.Date);

        var existing = await context.DailyRates
            .Where(rate => currencies.Contains(rate.Currency) && rate.Date >= earliest && rate.Date <= latest)
            .ToDictionaryAsync(rate => (rate.Currency, rate.Date), cancellationToken).ConfigureAwait(false);

        var written = 0;

        foreach (var rate in rates)
        {
            if (existing.TryGetValue((rate.Currency, rate.Date), out var stored))
            {
                if (stored.UnitsPerEuro == rate.UnitsPerEuro)
                {
                    continue;
                }

                // Se actualiza el publicado, pero eso no cambia ninguna cifra ya
                // calculada: los movimientos llevan su propio tipo congelado.
                context.Entry(stored).Property(entity => entity.UnitsPerEuro).CurrentValue = rate.UnitsPerEuro;
            }
            else
            {
                context.DailyRates.Add(rate);
            }

            written++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return written;
    }
}
