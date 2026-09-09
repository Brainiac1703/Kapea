using System.Globalization;
using System.Text.Json;

namespace Kapea.Infrastructure.Import.Kraken;

/// <summary>
/// Operación de TradesHistory. Kraken devuelve todos los números como cadena, así que
/// se leen con cultura invariante y no con el parseo implícito de JSON.
/// </summary>
public sealed record KrakenTrade(
    string TradeId,
    string Pair,
    DateTimeOffset Time,
    string Type,
    decimal Price,
    decimal Cost,
    decimal Fee,
    decimal Volume,
    string RawContent)
{
    public bool IsBuy => string.Equals(Type, "buy", StringComparison.OrdinalIgnoreCase);

    public static KrakenTrade From(string tradeId, JsonElement element) =>
        new(
            tradeId,
            KrakenJson.String(element, "pair"),
            KrakenJson.Time(element, "time"),
            KrakenJson.String(element, "type"),
            KrakenJson.Decimal(element, "price"),
            KrakenJson.Decimal(element, "cost"),
            KrakenJson.Decimal(element, "fee"),
            KrakenJson.Decimal(element, "vol"),
            element.GetRawText());
}

/// <summary>Apunte de Ledgers: ingresos, retiradas, comisiones, recompensas de staking.</summary>
public sealed record KrakenLedgerEntry(
    string LedgerId,
    string ReferenceId,
    DateTimeOffset Time,
    string Type,
    string SubType,
    string Asset,
    decimal Amount,
    decimal Fee,
    string RawContent)
{
    public static KrakenLedgerEntry From(string ledgerId, JsonElement element) =>
        new(
            ledgerId,
            KrakenJson.String(element, "refid"),
            KrakenJson.Time(element, "time"),
            KrakenJson.String(element, "type"),
            KrakenJson.String(element, "subtype"),
            KrakenJson.String(element, "asset"),
            KrakenJson.Decimal(element, "amount"),
            KrakenJson.Decimal(element, "fee"),
            element.GetRawText());
}

internal static class KrakenJson
{
    internal static string String(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    internal static decimal Decimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0m;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(
                value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m,
        };
    }

    /// <summary>Kraken marca el tiempo en segundos Unix con fracción decimal.</summary>
    internal static DateTimeOffset Time(JsonElement element, string name)
    {
        var seconds = Decimal(element, name);

        return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(seconds * 1000m, MidpointRounding.AwayFromZero));
    }
}
