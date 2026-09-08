using Kapea.Domain.Lots;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Posición abierta de un activo. La cantidad y el coste medio salen siempre de los
/// lotes; el valor actual y el resultado latente solo existen si hay precio de
/// mercado, y su ausencia se dice en lugar de disimularse con un cero.
/// </summary>
public sealed record OpenPosition(
    Guid AssetId,
    Quantity Quantity,
    Money CostInEuros,
    Money? MarketPriceInEuros,
    DateTimeOffset? PriceAsOf)
{
    /// <summary>Coste medio por unidad. Es lo que se compara con el precio de mercado.</summary>
    public Money AverageCostInEuros => Quantity.IsZero
        ? Money.Euros(0m)
        : CostInEuros / Quantity.Value;

    public bool HasMarketPrice => MarketPriceInEuros is not null;

    /// <summary>Valor de la posición al precio de mercado. Nulo si no hay precio.</summary>
    public Money? MarketValueInEuros => MarketPriceInEuros is { } price ? price * Quantity.Value : null;

    /// <summary>Ganancia o pérdida no realizada. Nula si no hay precio.</summary>
    public Money? UnrealisedResultInEuros =>
        MarketValueInEuros is { } value ? value - CostInEuros : null;

    /// <summary>
    /// Compone la posición a partir de los lotes con cantidad pendiente. Un activo sin
    /// lotes abiertos no tiene posición: sus resultados realizados siguen consultables,
    /// pero no aparece en la cartera.
    /// </summary>
    public static OpenPosition? From(
        Guid assetId,
        IEnumerable<Lot> lots,
        Money? marketPriceInEuros = null,
        DateTimeOffset? priceAsOf = null)
    {
        ArgumentNullException.ThrowIfNull(lots);

        var open = lots.Where(lot => !lot.IsExhausted).ToList();

        if (open.Count == 0)
        {
            return null;
        }

        var quantity = open.Aggregate(Quantity.Zero, (total, lot) => total + lot.RemainingQuantity);
        var cost = open.Aggregate(Money.Euros(0m), (total, lot) => total + lot.RemainingCost);

        return new OpenPosition(
            assetId,
            quantity,
            cost,
            marketPriceInEuros,
            marketPriceInEuros is null ? null : priceAsOf);
    }
}
