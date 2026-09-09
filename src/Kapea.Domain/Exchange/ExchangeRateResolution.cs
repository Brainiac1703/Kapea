using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Exchange;

/// <summary>
/// Resuelve el tipo aplicable a una fecha a partir de los tipos publicados.
/// Es lógica pura y vive en el dominio porque la regla de sustitución —usar el
/// último publicado anterior— tiene consecuencias fiscales y hay que poder
/// probarla sin base de datos ni red.
/// </summary>
public static class ExchangeRateResolution
{
    /// <summary>
    /// Busca el tipo de <paramref name="date"/>. Si ese día no hubo publicación
    /// (fin de semana, festivo del BCE), aplica el último publicado anterior y lo
    /// marca como sustituido. Devuelve null si no hay ningún tipo anterior.
    /// </summary>
    public static ExchangeRate? Resolve(
        Currency currency,
        DateOnly date,
        IEnumerable<DailyRate> publishedRates,
        string source = ExchangeRate.EuropeanCentralBank)
    {
        ArgumentNullException.ThrowIfNull(publishedRates);

        var applicable = publishedRates
            .Where(rate => rate.Currency == currency && rate.Date <= date)
            .OrderByDescending(rate => rate.Date)
            .FirstOrDefault();

        return applicable is null
            ? null
            : ExchangeRate.Create(currency, applicable.UnitsPerEuro, date, applicable.Date, source);
    }
}
