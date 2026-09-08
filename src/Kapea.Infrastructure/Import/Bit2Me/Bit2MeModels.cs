using System.Globalization;
using System.Text.Json;

namespace Kapea.Infrastructure.Import.Bit2Me;

/// <summary>Operación de contado. Bit2Me devuelve los importes como cadena y añade su valor en euros.</summary>
public sealed record Bit2MeTrade(
    string Id,
    string Symbol,
    string Side,
    decimal Price,
    decimal Amount,
    string PriceCurrency,
    string AmountCurrency,
    decimal Cost,
    decimal CostEuro,
    decimal FeeAmount,
    string FeeCurrency,
    DateTimeOffset CreatedAt,
    string RawContent)
{
    public bool IsBuy => string.Equals(Side, "buy", StringComparison.OrdinalIgnoreCase);

    public static Bit2MeTrade From(JsonElement element) =>
        new(
            Bit2MeJson.String(element, "id"),
            Bit2MeJson.String(element, "symbol"),
            Bit2MeJson.String(element, "side"),
            Bit2MeJson.Decimal(element, "price"),
            Bit2MeJson.Decimal(element, "amount"),
            Bit2MeJson.String(element, "priceCurrency"),
            Bit2MeJson.String(element, "amountCurrency"),
            Bit2MeJson.Decimal(element, "cost"),
            Bit2MeJson.Decimal(element, "costEuro"),
            Bit2MeJson.Decimal(element, "feeAmount"),
            Bit2MeJson.String(element, "feeCurrency"),
            Bit2MeJson.Time(element, "createdAt"),
            element.GetRawText());
}

/// <summary>Importe con su divisa, la forma en que Bit2Me expresa cantidades en el monedero.</summary>
public sealed record Bit2MeAmount(decimal Value, string Currency);

/// <summary>
/// Movimiento del monedero. Un mismo tipo cubre ingresos, retiradas, compras, ventas
/// y permutas; lo que las distingue es <see cref="Operation"/> y el par origen/destino.
/// </summary>
public sealed record Bit2MeWalletTransaction(
    string Id,
    string Operation,
    string Status,
    DateTimeOffset Date,
    Bit2MeAmount? Origin,
    Bit2MeAmount? Destination,
    Bit2MeAmount? Denomination,
    Bit2MeAmount? NetworkFee,
    string RawContent)
{
    public bool IsCompleted => string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);

    public static Bit2MeWalletTransaction From(JsonElement element)
    {
        // El tipo efectivo está en subtype cuando existe: type es la familia ('transfer')
        // y subtype la operación concreta ('purchase', 'swap'), que es la que importa.
        var subtype = Bit2MeJson.String(element, "subtype");
        var operation = subtype.Length > 0 ? subtype : Bit2MeJson.String(element, "type");

        return new Bit2MeWalletTransaction(
            Bit2MeJson.String(element, "id"),
            operation,
            Bit2MeJson.String(element, "status"),
            Bit2MeJson.Time(element, "completedAt") is var completed && completed != default
                ? completed
                : Bit2MeJson.Time(element, "date"),
            Bit2MeJson.Amount(element, "origin"),
            Bit2MeJson.Amount(element, "destination"),
            Bit2MeJson.Amount(element, "denomination"),
            ReadNetworkFee(element),
            element.GetRawText());
    }

    private static Bit2MeAmount? ReadNetworkFee(JsonElement element) =>
        element.TryGetProperty("fee", out var fee) && fee.ValueKind == JsonValueKind.Object
            ? Bit2MeJson.Amount(fee, "network")
            : null;
}

/// <summary>Monedero de rendimiento (Earn) de una divisa concreta.</summary>
public sealed record Bit2MeEarnWallet(string WalletId, string Currency);

/// <summary>Movimiento de un monedero Earn: aportaciones, retiradas y recompensas.</summary>
public sealed record Bit2MeEarnMovement(
    string MovementId,
    string Type,
    string WalletId,
    Bit2MeAmount Amount,
    DateTimeOffset CreatedAt,
    string RawContent)
{
    public static Bit2MeEarnMovement From(JsonElement element)
    {
        var amount = Bit2MeJson.Amount(element, "amount") ?? new Bit2MeAmount(0m, string.Empty);

        return new Bit2MeEarnMovement(
            Bit2MeJson.String(element, "movementId"),
            Bit2MeJson.String(element, "type"),
            Bit2MeJson.String(element, "walletId"),
            amount,
            Bit2MeJson.Time(element, "createdAt"),
            element.GetRawText());
    }
}

internal static class Bit2MeJson
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

    internal static DateTimeOffset Time(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : default;

    /// <summary>Lee un objeto con importe y divisa, admitiendo las dos formas que usa Bit2Me: amount y value.</summary>
    internal static Bit2MeAmount? Amount(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var currency = String(node, "currency");

        if (currency.Length == 0)
        {
            return null;
        }

        var value = node.TryGetProperty("amount", out _) ? Decimal(node, "amount") : Decimal(node, "value");

        return new Bit2MeAmount(value, currency);
    }
}
