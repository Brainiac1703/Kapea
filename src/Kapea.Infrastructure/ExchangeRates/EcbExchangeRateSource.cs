using Kapea.Application.Abstractions;
using Kapea.Domain.Exchange;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.ExchangeRates;

/// <summary>
/// Descarga los tipos de referencia diarios del BCE. Es la fuente que admite la AEAT
/// y la que un asesor fiscal espera ver citada en un informe.
/// </summary>
/// <remarks>
/// El BCE no ofrece descarga por rango: publica el fichero del día, el de los últimos
/// noventa días y el histórico completo. Se elige el menor de los tres que cubre el
/// rango pedido y se filtra en memoria, en lugar de traer siempre el histórico entero.
/// </remarks>
public sealed class EcbExchangeRateSource(HttpClient httpClient, ILogger<EcbExchangeRateSource> logger)
    : IExchangeRateSource
{
    internal const string DailyPath = "stats/eurofxref/eurofxref-daily.xml";
    internal const string NinetyDayPath = "stats/eurofxref/eurofxref-hist-90d.xml";
    internal const string HistoryPath = "stats/eurofxref/eurofxref-hist.xml";

    public string Name => ExchangeRate.EuropeanCentralBank;

    public async Task<IReadOnlyList<DailyRate>> FetchAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            throw new ArgumentException("El inicio del rango no puede ser posterior a su fin.", nameof(from));
        }

        var path = ChoosePath(from, DateOnly.FromDateTime(DateTime.UtcNow));

        logger.LogInformation("Descargando tipos del BCE desde {Ruta} para el rango {Desde}-{Hasta}.", path, from, to);

        using var response = await httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var rates = EcbRatesParser.Parse(content)
            .Where(rate => rate.Date >= from && rate.Date <= to)
            .ToList();

        logger.LogInformation("Descargados {Total} tipos del BCE.", rates.Count);

        return rates;
    }

    internal static string ChoosePath(DateOnly from, DateOnly today)
    {
        var age = today.DayNumber - from.DayNumber;

        return age switch
        {
            <= 0 => DailyPath,
            <= 85 => NinetyDayPath,
            _ => HistoryPath,
        };
    }
}
