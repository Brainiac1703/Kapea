using Kapea.Domain.MarketData;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>Cuántas unidades de un activo había un día y cuánto valían.</summary>
/// <param name="ValueInEuros">Nulo cuando ese día no hay precio: la ausencia se dice.</param>
public sealed record AssetDay(Guid AssetId, Quantity Quantity, Money? PriceInEuros, Money? ValueInEuros);

/// <summary>Un día de la cartera.</summary>
/// <param name="ValueInEuros">Valor de lo que se pudo valorar ese día.</param>
/// <param name="NetContributionInEuros">Dinero aportado menos retirado ese día.</param>
/// <param name="IsComplete">Falso si falta el precio de algún activo con posición.</param>
public sealed record PortfolioDay(
    DateOnly Date,
    IReadOnlyList<AssetDay> Assets,
    Money ValueInEuros,
    Money NetContributionInEuros,
    bool IsComplete);

/// <summary>
/// Reconstruye lo que valía la cartera cada día.
/// </summary>
/// <remarks>
/// Las unidades salen de recorrer los movimientos en orden, igual que hace el motor
/// FIFO, y el valor de la serie de precios de ese día. No se guarda nada: una foto
/// diaria guardada quedaría vieja en cuanto entrara una importación con fecha anterior,
/// y nadie se enteraría.
///
/// Un día sin precio no se rellena con el anterior ni se interpola. Se marca incompleto
/// y se dice qué falta: una línea recta inventada se lee igual que una cotización real.
/// </remarks>
public static class PortfolioHistory
{
    public static IReadOnlyList<PortfolioDay> Build(
        IEnumerable<ValuedTransaction> transactions,
        IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>> prices,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(prices);

        var ordered = transactions
            .Where(valued => !valued.IsUnresolved)
            .OrderBy(valued => valued.OccurredAt.Instant)
            .ToList();

        var byDay = prices.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToDictionary(price => price.Date, price => price.PriceInEuros));

        var quantities = new Dictionary<Guid, Quantity>();
        var days = new List<PortfolioDay>();
        var next = 0;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var contribution = Money.Euros(0m);

            // Los movimientos del propio día ya cuentan: comprar hoy significa tener hoy.
            while (next < ordered.Count && DateOf(ordered[next]) <= day)
            {
                Apply(ordered[next], quantities, ref contribution);
                next++;
            }

            days.Add(Day(day, quantities, byDay, contribution));
        }

        return days;
    }

    private static DateOnly DateOf(ValuedTransaction valued) =>
        DateOnly.FromDateTime(valued.OccurredAt.Instant.UtcDateTime);

    private static void Apply(
        ValuedTransaction valued,
        Dictionary<Guid, Quantity> quantities,
        ref Money contribution)
    {
        // Lo aportado y lo retirado se lleva aparte del valor: un ingreso seguido de una
        // compra sube la cartera sin que nadie haya ganado nada, y confundir las dos
        // cosas convierte el ahorro en rendimiento.
        if (valued.Type is TransactionType.Deposit)
        {
            contribution += valued.GrossAmountInEuros;
        }
        else if (valued.Type is TransactionType.Withdrawal)
        {
            contribution -= valued.GrossAmountInEuros;
        }

        if (valued.AssetId is not { } assetId)
        {
            return;
        }

        var held = quantities.GetValueOrDefault(assetId, Quantity.Zero);

        quantities[assetId] = valued.Type switch
        {
            TransactionType.Buy or TransactionType.Dividend or TransactionType.Interest or TransactionType.Reward =>
                held + valued.Quantity,
            TransactionType.Sell => Subtract(held, valued.Quantity),

            // Un traspaso interno mueve el activo de cuenta, no de cartera: la pata de
            // salida y la de entrada se anulan y la cantidad total no cambia.
            TransactionType.Transfer => held,
            TransactionType.Split when valued.SplitRatio is { } ratio => held * ratio,
            _ => held,
        };
    }

    /// <summary>Resta sin bajar de cero: un histórico incompleto no puede dejar cantidades negativas.</summary>
    private static Quantity Subtract(Quantity held, Quantity sold) =>
        sold.Value >= held.Value ? Quantity.Zero : held - sold;

    private static PortfolioDay Day(
        DateOnly date,
        Dictionary<Guid, Quantity> quantities,
        Dictionary<Guid, Dictionary<DateOnly, decimal>> prices,
        Money contribution)
    {
        var assets = new List<AssetDay>();
        var value = Money.Euros(0m);
        var complete = true;

        foreach (var (assetId, quantity) in quantities.Where(entry => !entry.Value.IsZero).OrderBy(entry => entry.Key))
        {
            if (prices.TryGetValue(assetId, out var series) && series.TryGetValue(date, out var price))
            {
                var valued = Money.Euros(price * quantity.Value);
                value += valued;
                assets.Add(new AssetDay(assetId, quantity, Money.Euros(price), valued));
            }
            else
            {
                complete = false;
                assets.Add(new AssetDay(assetId, quantity, null, null));
            }
        }

        return new PortfolioDay(date, assets, value, contribution, complete);
    }
}
