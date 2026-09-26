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

/// <summary>Un día de la serie de un solo activo.</summary>
public sealed record AssetHistoryDay(DateOnly Date, Quantity Quantity, Money? PriceInEuros, Money? ValueInEuros);

/// <summary>Lo que valía cada clase de activo un día.</summary>
/// <param name="ValueByClass">Valor por clase. Una clase sin posición ese día no aparece.</param>
public sealed record ClassHistoryDay(
    DateOnly Date,
    IReadOnlyDictionary<Assets.AssetClass, Money> ValueByClass,
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

    /// <summary>
    /// La serie de un solo activo: su cotización, y lo que se tenía de él.
    /// </summary>
    /// <remarks>
    /// Son dos cosas distintas y por eso vienen de dos sitios. Las unidades y el valor
    /// salen de la posición, y se filtran de la serie de la cartera para que la cifra de
    /// un activo y la del total salgan del mismo sitio: dos caminos acaban divergiendo.
    ///
    /// La cotización no depende de tener el activo. Sacarla de la posición —como se hacía
    /// antes— dejaba sin precio los días sin posición, y sin serie entera lo que sólo se
    /// vigila. Eso vaciaba la gráfica de casi todo lo que el usuario sigue, que es
    /// justamente donde hace falta para decidir si entrar.
    /// </remarks>
    /// <param name="quotes">Cotización por día, exista posición o no. Un día que falte no tiene precio.</param>
    public static IReadOnlyList<AssetHistoryDay> ForAsset(
        IEnumerable<PortfolioDay> days,
        Guid assetId,
        IReadOnlyDictionary<DateOnly, Money>? quotes = null)
    {
        ArgumentNullException.ThrowIfNull(days);

        return
        [
            .. days.Select(day =>
            {
                var held = day.Assets.FirstOrDefault(asset => asset.AssetId == assetId);

                // La posición manda sobre la cotización cuando la hay: es la que entra en
                // el valor de la cartera, y la serie tiene que decir lo mismo que el total.
                var quote = held?.PriceInEuros
                    ?? (quotes is not null && quotes.TryGetValue(day.Date, out var price) ? price : null);

                return new AssetHistoryDay(
                    day.Date,
                    held?.Quantity ?? Quantity.Zero,
                    quote,
                    held?.ValueInEuros);
            }),
        ];
    }

    /// <summary>
    /// El reparto por clase de activo a lo largo del tiempo.
    /// </summary>
    /// <remarks>
    /// Los grupos salen de lo que haya cada día, no de una lista escrita: una clase que
    /// se incorpore más tarde aparece sola desde su primera adquisición.
    /// </remarks>
    public static IReadOnlyList<ClassHistoryDay> ByClass(
        IEnumerable<PortfolioDay> days,
        IReadOnlyDictionary<Guid, Assets.AssetClass> classes)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(classes);

        return
        [
            .. days.Select(day =>
            {
                var byClass = new Dictionary<Assets.AssetClass, Money>();

                foreach (var asset in day.Assets)
                {
                    if (asset.ValueInEuros is not { } value || !classes.TryGetValue(asset.AssetId, out var assetClass))
                    {
                        continue;
                    }

                    byClass[assetClass] = byClass.TryGetValue(assetClass, out var running)
                        ? running + value
                        : value;
                }

                return new ClassHistoryDay(day.Date, byClass, day.IsComplete);
            }),
        ];
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
        //
        // Qué es aportar lo decide una sola definición, la misma que usa el resumen de
        // la cartera: contar aquí también los traspasos entre cuentas propias y las
        // entradas de activos daba dos cifras distintas del mismo dinero, y la de aquí
        // era la equivocada.
        if (ContributedCapitalCalculator.IsContribution(valued))
        {
            contribution += valued.Type is TransactionType.Deposit
                ? valued.GrossAmountInEuros
                : -valued.GrossAmountInEuros;
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
