using Kapea.Domain.MarketData;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>Cuántas unidades de un activo había un día y cuánto valían.</summary>
/// <param name="ValueInEuros">Nulo cuando ese día no hay precio: la ausencia se dice.</param>
/// <param name="CarriedFrom">
/// De qué día viene el precio cuando no es del día valorado. Nulo cuando el mercado
/// cotizó, que es lo normal: sólo se rellena al arrastrar un cierre anterior.
/// </param>
public sealed record AssetDay(
    Guid AssetId,
    Quantity Quantity,
    Money? PriceInEuros,
    Money? ValueInEuros,
    DateOnly? CarriedFrom = null)
{
    /// <summary>El precio viene del último cierre porque ese día el mercado no cotizó.</summary>
    public bool IsCarried => CarriedFrom is not null;
}

/// <summary>Un día de la cartera.</summary>
/// <param name="ValueInEuros">Valor de lo que se pudo valorar ese día.</param>
/// <param name="NetContributionInEuros">Dinero aportado menos retirado ese día.</param>
/// <param name="IsComplete">
/// Falso si falta el precio de algún activo con posición cuyo mercado sí cotizó ese día.
/// Un mercado cerrado no deja el día incompleto: su posición se valora al último cierre.
/// </param>
public sealed record PortfolioDay(
    DateOnly Date,
    IReadOnlyList<AssetDay> Assets,
    Money ValueInEuros,
    Money NetContributionInEuros,
    bool IsComplete)
{
    /// <summary>Alguna posición se ha valorado con un cierre anterior.</summary>
    public bool HasCarriedPrices => Assets.Any(asset => asset.IsCarried);
}

/// <summary>Un día de la serie de un solo activo.</summary>
/// <param name="CarriedFrom">De qué día viene el precio, cuando no es del día valorado.</param>
public sealed record AssetHistoryDay(
    DateOnly Date,
    Quantity Quantity,
    Money? PriceInEuros,
    Money? ValueInEuros,
    DateOnly? CarriedFrom = null);

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
///
/// La excepción es el mercado cerrado, que no es un dato que falte. Un sábado una acción
/// vale lo que valía el viernes al cierre: el mercado no cotizó, pero la posición no dejó
/// de valer. Esos días se valoran con el último cierre y se marcan como arrastrados, para
/// no hacer creer que el mercado se movió. El arrastre vive aquí y no en la serie de
/// precios, de modo que los indicadores y el backtest nunca ven un precio que nadie
/// cotizó.
/// </remarks>
public static class PortfolioHistory
{
    /// <param name="classes">
    /// Clase de cada activo, para deducir qué días no cotizó su mercado. Sin ella no se
    /// arrastra nada y la serie sale como antes.
    /// </param>
    /// <param name="priorCloses">
    /// Último cierre de cada activo anterior al rango. Hace falta porque el primer día
    /// pedido puede caer en fin de semana y no habría de dónde arrastrar.
    /// </param>
    public static IReadOnlyList<PortfolioDay> Build(
        IEnumerable<ValuedTransaction> transactions,
        IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>> prices,
        DateOnly from,
        DateOnly to,
        IReadOnlyDictionary<Guid, Assets.AssetClass>? classes = null,
        IReadOnlyDictionary<Guid, DailyPrice>? priorCloses = null)
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

        var closures = classes is null
            ? MarketClosures.None
            : MarketClosures.Of(prices, classes, from, to);

        // Lo último conocido de cada activo, que avanza según se recorren los días. Nace
        // con el cierre anterior al rango para que un primer día en sábado tenga de dónde
        // tirar.
        var last = new Dictionary<Guid, (DateOnly Date, decimal Price)>();

        if (priorCloses is not null)
        {
            foreach (var (assetId, price) in priorCloses)
            {
                last[assetId] = (price.Date, price.PriceInEuros);
            }
        }

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

            days.Add(Day(day, quantities, byDay, contribution, closures, classes, last));
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
                    held?.ValueInEuros,
                    held?.CarriedFrom);
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
        Money contribution,
        MarketClosures closures,
        IReadOnlyDictionary<Guid, Assets.AssetClass>? classes,
        Dictionary<Guid, (DateOnly Date, decimal Price)> last)
    {
        var assets = new List<AssetDay>();
        var value = Money.Euros(0m);
        var complete = true;

        foreach (var (assetId, quantity) in quantities.Where(entry => !entry.Value.IsZero).OrderBy(entry => entry.Key))
        {
            if (prices.TryGetValue(assetId, out var series) && series.TryGetValue(date, out var price))
            {
                last[assetId] = (date, price);

                var valued = Money.Euros(price * quantity.Value);
                value += valued;
                assets.Add(new AssetDay(assetId, quantity, Money.Euros(price), valued));

                continue;
            }

            // El mercado cerrado no es un dato que falte: la posición vale lo del último
            // cierre. Sin cierre anterior no hay nada que arrastrar, y el día queda
            // incompleto como cualquier otra laguna.
            var closed = classes is not null
                && classes.TryGetValue(assetId, out var assetClass)
                && closures.WasClosed(assetClass, date);

            if (closed && last.TryGetValue(assetId, out var previous))
            {
                var valued = Money.Euros(previous.Price * quantity.Value);
                value += valued;
                assets.Add(new AssetDay(assetId, quantity, Money.Euros(previous.Price), valued, previous.Date));

                continue;
            }

            complete = false;
            assets.Add(new AssetDay(assetId, quantity, null, null));
        }

        return new PortfolioDay(date, assets, value, contribution, complete);
    }
}
