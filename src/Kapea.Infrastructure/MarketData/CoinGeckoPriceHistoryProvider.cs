using System.Text.Json;
using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.MarketData;

/// <summary>
/// Series históricas desde CoinGecko.
/// </summary>
/// <remarks>
/// Cubre tokens que Yahoo no cotiza, pero su capa gratuita solo sirve los últimos 365
/// días: comprobado el 12 de septiembre de 2026, cualquier fecha anterior responde con
/// el error 10012 tanto por rango como por día suelto. Por eso es el respaldo y no la
/// fuente principal, y por eso lo anterior a esa ventana se pide sin esperanza y se
/// trata como falta de cobertura, no como fallo.
///
/// El rango devuelve puntos horarios cuando se piden pocos días, así que el cierre del
/// día es el último punto de cada día.
/// </remarks>
public sealed class CoinGeckoPriceHistoryProvider(
    HttpClient httpClient,
    TimeProvider timeProvider,
    ILogger<CoinGeckoPriceHistoryProvider> logger) : IPriceHistoryProvider
{
    /// <summary>Hasta dónde llega hacia atrás la capa gratuita.</summary>
    public static readonly int FreeHistoryDays = 365;

    public string Name => "CoinGecko";

    public async Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
        PriceHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Class != AssetClass.Crypto
            || !CoinGeckoMarketPriceProvider.CoinIds.TryGetValue(request.CanonicalSymbol, out var coinId))
        {
            return [];
        }

        // Pedir fuera de la ventana gratuita no devuelve nada, así que se recorta antes
        // de preguntar: gastar la petición para recibir un error no ayuda a nadie.
        var earliest = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-FreeHistoryDays + 1);
        var from = request.From < earliest ? earliest : request.From;

        if (request.To < from)
        {
            return [];
        }

        var query = $"api/v3/coins/{Uri.EscapeDataString(coinId)}/market_chart/range?vs_currency=eur" +
            $"&from={Instant(from)}&to={Instant(request.To.AddDays(1))}";

        try
        {
            using var response = await httpClient.GetAsync(query, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);

            return Read(document, request, from);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(
                exception,
                "CoinGecko no ha devuelto el histórico de {Simbolo}; se reintentará.",
                request.CanonicalSymbol);

            return [];
        }
    }

    private static long Instant(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

    private static List<DailyPrice> Read(JsonDocument document, PriceHistoryRequest request, DateOnly from)
    {
        var prices = new List<DailyPrice>();

        if (!document.RootElement.TryGetProperty("prices", out var points)
            || points.ValueKind != JsonValueKind.Array)
        {
            return prices;
        }

        // El último punto de cada día es su cierre. Con rangos cortos CoinGecko devuelve
        // puntos horarios, y quedarse con el primero daría el precio de la madrugada.
        var byDay = new Dictionary<DateOnly, decimal>();

        foreach (var point in points.EnumerateArray())
        {
            if (point.ValueKind != JsonValueKind.Array
                || point.GetArrayLength() < 2
                || !point[0].TryGetInt64(out var milliseconds)
                || point[1].ValueKind != JsonValueKind.Number
                || !point[1].TryGetDecimal(out var price)
                || price <= 0m)
            {
                continue;
            }

            var day = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime);

            if (day < from || day > request.To)
            {
                continue;
            }

            byDay[day] = price;
        }

        prices.AddRange(byDay
            .OrderBy(entry => entry.Key)
            .Select(entry => new DailyPrice(request.AssetId, entry.Key, entry.Value, "CoinGecko")));

        return prices;
    }
}
