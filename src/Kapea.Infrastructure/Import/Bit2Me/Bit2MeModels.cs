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
/// <param name="EuroRate">
/// Cuántos euros vale una unidad, cuando Bit2Me lo aporta.
/// </param>
/// <remarks>
/// Cada lado de una permuta trae su cambio contra el euro. Sin él, una venta de cripto
/// por cripto no tendría importe: el campo que valora el movimiento entero viene en la
/// moneda de origen, no en euros, así que la operación entraría con cero de ingreso y
/// eso falsearía el resultado del ejercicio.
/// </remarks>
public sealed record Bit2MeAmount(decimal Value, string Currency, decimal? EuroRate = null)
{
    /// <summary>Valor en euros, si se puede saber.</summary>
    public decimal? ValueInEuros => EuroRate is { } rate ? Math.Abs(Value) * rate : null;
}

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

    /// <summary>
    /// Nombres posibles de la operación, del más específico al más general.
    /// </summary>
    /// <remarks>
    /// Bit2Me reparte el significado entre dos campos y no siempre igual. A veces
    /// 'subtype' es la operación entera ('swap', 'purchase') y 'type' solo la familia;
    /// otras veces hace falta leer los dos juntos, como en una retirada hacia Earn, que
    /// llega como type 'withdrawal' y subtype 'earn' y no es una retirada de verdad sino
    /// un traspaso entre productos del mismo usuario.
    ///
    /// Quedarse con uno de los dos hacía que esos movimientos entraran sin clasificar.
    /// </remarks>
    public IReadOnlyList<string> Operations { get; private init; } = [];

    public static Bit2MeWalletTransaction From(JsonElement element)
    {
        var type = Bit2MeJson.String(element, "type");
        var subtype = Bit2MeJson.String(element, "subtype");

        List<string> operations = [];

        if (type.Length > 0 && subtype.Length > 0)
        {
            operations.Add($"{type}-{subtype}");
        }

        if (subtype.Length > 0)
        {
            operations.Add(subtype);
        }

        if (type.Length > 0)
        {
            operations.Add(type);
        }

        return new Bit2MeWalletTransaction(
            Bit2MeJson.String(element, "id"),
            operations.FirstOrDefault() ?? string.Empty,
            Bit2MeJson.String(element, "status"),
            Bit2MeJson.Time(element, "completedAt") is var completed && completed != default
                ? completed
                : Bit2MeJson.Time(element, "date"),
            Bit2MeJson.Amount(element, "origin"),
            Bit2MeJson.Amount(element, "destination"),
            Bit2MeJson.Amount(element, "denomination"),
            ReadNetworkFee(element),
            element.GetRawText())
        {
            Operations = operations,
        };
    }

    private static Bit2MeAmount? ReadNetworkFee(JsonElement element) =>
        element.TryGetProperty("fee", out var fee) && fee.ValueKind == JsonValueKind.Object
            ? Bit2MeJson.Amount(fee, "network")
            : null;
}

/// <summary>Monedero de rendimiento (Earn) de una divisa concreta.</summary>
public sealed record Bit2MeEarnWallet(string WalletId, string Currency);

/// <summary>Movimiento de un monedero Earn: aportaciones, retiradas y recompensas.</summary>
/// <param name="ValueInEuros">
/// Lo que valía al cobrarlo, que es por lo que tributa y lo que cuesta lo entregado.
/// </param>
/// <remarks>
/// Bit2Me lo manda en «convertedAmount». Sin él, la cantidad de cripto acababa usada
/// como importe: dos mil setecientos B2M de recompensa parecían dos mil setecientos
/// euros de rendimiento y de coste.
/// </remarks>
public sealed record Bit2MeEarnMovement(
    string MovementId,
    string Type,
    string WalletId,
    Bit2MeAmount Amount,
    decimal? ValueInEuros,
    DateTimeOffset CreatedAt,
    string RawContent)
{
    public static Bit2MeEarnMovement From(JsonElement element)
    {
        var amount = Bit2MeJson.Amount(element, "amount") ?? new Bit2MeAmount(0m, string.Empty);
        var converted = Bit2MeJson.Amount(element, "convertedAmount");

        var euros = converted is { } value
            && string.Equals(value.Currency, "EUR", StringComparison.OrdinalIgnoreCase)
                ? Math.Abs(value.Value)
                : amount.ValueInEuros;

        return new Bit2MeEarnMovement(
            Bit2MeJson.String(element, "movementId"),
            Bit2MeJson.String(element, "type"),
            Bit2MeJson.String(element, "walletId"),
            amount,
            euros,
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

        return new Bit2MeAmount(value, currency, EuroRate(node, currency));
    }

    /// <summary>
    /// Cambio contra el euro que acompaña al importe, si lo hay y es contra el euro.
    /// </summary>
    /// <remarks>
    /// Se comprueba el par: Bit2Me también manda cambios de una moneda contra sí misma,
    /// y darlos por buenos convertiría una cantidad de cripto en euros.
    /// </remarks>
    private static decimal? EuroRate(JsonElement node, string currency)
    {
        if (!node.TryGetProperty("rate", out var rate) || rate.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!rate.TryGetProperty("pair", out var pair) || pair.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var quote = String(pair, "quote");
        var basis = String(pair, "base");

        if (!string.Equals(quote, "EUR", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(basis, currency, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var value = Decimal(rate, "value");

        return value > 0m ? value : null;
    }
}
