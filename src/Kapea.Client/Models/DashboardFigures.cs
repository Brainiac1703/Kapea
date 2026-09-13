using Kapea.Shared.Contracts;

namespace Kapea.Client.Models;

/// <summary>Variación de un día: cuánto cambió el valor sin contar el dinero que entró o salió.</summary>
public sealed record DailyChange(DateOnly Date, decimal Amount, decimal? Rate);

/// <summary>Cifras del inicio que se derivan de lo que ya entrega la API.</summary>
public static class DashboardFigures
{
    /// <summary>
    /// La variación del último día con precio frente al anterior con precio.
    /// </summary>
    /// <remarks>
    /// Se descuenta lo aportado ese día: meter cien euros no es ganar cien euros, y sin
    /// descontarlo el día de una compra saldría siempre como un buen día.
    /// Los días incompletos se saltan en lugar de compararse: un día sin precio de un
    /// activo valdría de menos y la variación sería una caída que no ocurrió.
    /// </remarks>
    public static DailyChange? LastDay(IReadOnlyList<PortfolioHistoryDayResponse> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        var complete = days.Where(day => day.IsComplete).OrderBy(day => day.Date).ToList();

        if (complete.Count < 2)
        {
            return null;
        }

        var last = complete[^1];
        var previous = complete[^2];

        var contributedSince = days
            .Where(day => day.Date > previous.Date && day.Date <= last.Date)
            .Sum(day => day.ContributionInEuros);

        var amount = last.ValueInEuros - previous.ValueInEuros - contributedSince;
        decimal? rate = previous.ValueInEuros > 0m ? amount / previous.ValueInEuros : null;

        return new DailyChange(last.Date, amount, rate);
    }
}
