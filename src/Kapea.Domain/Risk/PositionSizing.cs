using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Risk;

/// <summary>
/// Cuánto comprar de un activo, dado lo que se acepta perder si sale mal.
/// </summary>
/// <param name="RiskInEuros">Lo que se pierde si el precio llega al nivel de salida.</param>
/// <param name="ExceedsPositionCap">La compra dejaría la posición por encima de su tope.</param>
/// <param name="AllowedByCap">Lo máximo que caber sin pasarse del tope, cuando lo hay.</param>
public sealed record PositionSize(
    Quantity Quantity,
    Money AmountInEuros,
    Money RiskInEuros,
    bool ExceedsPositionCap,
    Money? AllowedByCap);

/// <summary>
/// Calcula el tamaño de una posición a partir del riesgo, no del capricho.
/// </summary>
/// <remarks>
/// Dos activos con el mismo capital asignado necesitan tamaños distintos si uno se mueve
/// el doble que el otro: arriesgar lo mismo en los dos es lo que hace comparables las
/// decisiones, y es lo que más rinde a largo plazo.
///
/// Sin nivel de salida no se propone nada. Suponerlo sería inventar el riesgo, que es
/// justamente el dato que hace falta.
/// </remarks>
public static class PositionSizing
{
    /// <param name="capital">Patrimonio total, del que se calcula el riesgo aceptado.</param>
    /// <param name="riskPerTrade">Parte del patrimonio que se acepta perder, en tanto por uno.</param>
    /// <param name="entryPrice">Precio al que se entraría.</param>
    /// <param name="stopPrice">Precio al que se saldría si sale mal.</param>
    /// <param name="positionCap">Tope de la posición sobre el patrimonio, si lo hay.</param>
    public static PositionSize? For(
        Money capital,
        decimal riskPerTrade,
        Money entryPrice,
        Money? stopPrice,
        decimal? positionCap = null)
    {
        if (riskPerTrade <= 0m || entryPrice.Amount <= 0m || capital.Amount <= 0m)
        {
            return null;
        }

        if (stopPrice is not { } stop || stop.Amount <= 0m || stop.Amount >= entryPrice.Amount)
        {
            return null;
        }

        var distance = entryPrice.Amount - stop.Amount;
        var risk = capital.Amount * riskPerTrade;
        var quantity = risk / distance;
        var amount = quantity * entryPrice.Amount;

        Money? allowed = positionCap is { } cap and > 0m ? Money.Euros(capital.Amount * cap) : null;
        var exceeds = allowed is { } limit && amount > limit.Amount;

        return new PositionSize(
            new Quantity(quantity),
            Money.Euros(amount),
            Money.Euros(risk),
            exceeds,
            allowed);
    }
}
