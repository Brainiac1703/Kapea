using System.Text.Json;
using Kapea.Application.Abstractions;
using Kapea.Domain.MarketData;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Series históricas desde Yahoo Finance.
/// </summary>
/// <remarks>
/// Es la fuente principal del histórico porque entrega años de cierres diarios en euros,
/// también de criptomonedas, sin clave ni cuota declarada. Lo que no cubre son los tokens
/// pequeños, que quedan para CoinGecko dentro de su ventana de un año.
///
/// Un símbolo que no conoce devuelve «No data found», y eso no es un fallo: es que no lo
/// cubre. Se trata como serie vacía para que el resto de activos se descarguen igual.
/// </remarks>
public sealed class YahooPriceHistoryProvider(
    HttpClient httpClient,
    IExchangeRateProvider exchangeRates,
    ILogger<YahooPriceHistoryProvider> logger) : IPriceHistoryProvider
{
    /// <summary>Origen de un precio que hubo que pasar a euros.</summary>
    internal const string ConvertedSource = "Yahoo+BCE";

    public string Name => "Yahoo";

    public async Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
        PriceHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.To < request.From)
        {
            return [];
        }

        var symbol = YahooSymbols.ToYahoo(request.CanonicalSymbol, request.Class);

        // Yahoo acota por instante y excluye el extremo superior, así que se pide desde
        // el principio del primer día hasta el final del último.
        var from = Instant(request.From);
        var to = Instant(request.To.AddDays(1));

        var query = $"v8/finance/chart/{Uri.EscapeDataString(symbol)}" +
            $"?period1={from}&period2={to}&interval=1d";

        try
        {
            using var response = await httpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);

            // Un símbolo que Yahoo no conoce responde 404. No es un fallo del sistema:
            // es un activo que esta fuente no cubre.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);

            var currency = CurrencyOf(document);

            if (currency is null)
            {
                return [];
            }

            var closes = Read(document, request);

            return currency.Value.IsEuro
                ? closes
                : await ToEurosAsync(closes, currency.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Lo ya descargado de otros activos se conserva; este se reintenta en la
            // siguiente vuelta. Un hueco es preferible a una serie inventada.
            logger.LogWarning(
                exception, "Yahoo no ha devuelto el histórico de {Simbolo}; se reintentará.", symbol);

            return [];
        }
    }

    private static long Instant(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>
    /// Pasa a euros una serie cotizada en otra divisa, al tipo del BCE de cada día.
    /// </summary>
    /// <remarks>
    /// Yahoo no cotiza en euros los tokens pequeños, y es la única fuente que llega más
    /// atrás de un año. Se convierte con el mismo tipo con el que se valoran los
    /// movimientos, que está guardado y es contrastable, y el origen lo dice para que
    /// nadie confunda un precio convertido con uno cotizado.
    ///
    /// Un día sin tipo se queda fuera: el BCE no publica fines de semana, así que se
    /// toma el último publicado, y si no hay ninguno el día no se inventa.
    /// </remarks>
    private async Task<IReadOnlyList<DailyPrice>> ToEurosAsync(
        List<DailyPrice> closes,
        Currency currency,
        CancellationToken cancellationToken)
    {
        var converted = new List<DailyPrice>(closes.Count);

        foreach (var close in closes)
        {
            var rate = await exchangeRates.ResolveAsync(currency, close.Date, cancellationToken).ConfigureAwait(false);

            if (rate is null)
            {
                continue;
            }

            converted.Add(close with
            {
                PriceInEuros = rate.ToEuros(new Money(close.PriceInEuros, currency)).Amount,
                Source = ConvertedSource,
            });
        }

        if (converted.Count < closes.Count)
        {
            logger.LogInformation(
                "Faltan tipos de cambio para {Dias} días de una serie en {Divisa}.",
                closes.Count - converted.Count, currency.Code);
        }

        return converted;
    }

    /// <summary>Divisa en la que Yahoo cotiza la serie, o su ausencia si no la dice.</summary>
    private static Currency? CurrencyOf(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("chart", out var chart)
            || !chart.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0
            || !results[0].TryGetProperty("meta", out var meta)
            || !meta.TryGetProperty("currency", out var currency)
            || currency.GetString() is not { Length: > 0 } code)
        {
            return null;
        }

        return Currency.FromCode(code);
    }

    private static List<DailyPrice> Read(JsonDocument document, PriceHistoryRequest request)
    {
        var prices = new List<DailyPrice>();

        if (!document.RootElement.TryGetProperty("chart", out var chart)
            || !chart.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return prices;
        }

        var result = results[0];

        if (!result.TryGetProperty("timestamp", out var timestamps)
            || timestamps.ValueKind != JsonValueKind.Array
            || !result.TryGetProperty("indicators", out var indicators)
            || !indicators.TryGetProperty("quote", out var quotes)
            || quotes.ValueKind != JsonValueKind.Array
            || quotes.GetArrayLength() == 0
            || !quotes[0].TryGetProperty("close", out var closes)
            || closes.ValueKind != JsonValueKind.Array)
        {
            return prices;
        }

        var days = Math.Min(timestamps.GetArrayLength(), closes.GetArrayLength());

        for (var index = 0; index < days; index++)
        {
            // Un día sin cierre viene como nulo. Se salta: la ausencia se dice dejando
            // el día fuera de la serie, no rellenándolo con el anterior.
            if (closes[index].ValueKind != JsonValueKind.Number
                || !closes[index].TryGetDecimal(out var close)
                || close <= 0m
                || !timestamps[index].TryGetInt64(out var seconds))
            {
                continue;
            }

            var day = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime);

            if (day < request.From || day > request.To)
            {
                continue;
            }

            prices.Add(new DailyPrice(request.AssetId, day, close, "Yahoo"));
        }

        return prices;
    }
}
