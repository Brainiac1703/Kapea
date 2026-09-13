using Kapea.Domain.Indicators;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Strategies;

/// <summary>
/// Lo que cuesta operar en una plataforma.
/// </summary>
/// <param name="RateInsidePrice">
/// Parte que la plataforma se queda dentro del precio, en tanto por uno.
/// </param>
/// <param name="FixedFee">Comisión aparte, cuando la plataforma la cobre así.</param>
/// <remarks>
/// Sale de la plataforma y no del código: el día que cambie sus tarifas, todas las
/// simulaciones anteriores mentirían sin avisar.
/// </remarks>
public sealed record TradingCost(decimal RateInsidePrice, Money FixedFee)
{
    public static TradingCost None => new(0m, Money.Euros(0m));

    /// <summary>Lo que cuesta mover un importe, en cualquiera de los dos sentidos.</summary>
    public Money On(Money amount) => Money.Euros(Math.Abs(amount.Amount) * RateInsidePrice) + FixedFee;
}

/// <summary>
/// Los tramos del ahorro con los que se estima el impuesto.
/// </summary>
/// <remarks>
/// Se declaran como dato porque cambian por ley. Es una estimación para comparar
/// sistemas entre sí, no una liquidación.
/// </remarks>
public sealed record SavingsTax(IReadOnlyList<(decimal UpTo, decimal Rate)> Brackets)
{
    /// <summary>Tramos vigentes en España al escribir esto.</summary>
    public static SavingsTax Spain2026 => new(
    [
        (6_000m, 0.19m),
        (50_000m, 0.21m),
        (200_000m, 0.23m),
        (300_000m, 0.27m),
        (decimal.MaxValue, 0.30m),
    ]);

    public static SavingsTax None => new([(decimal.MaxValue, 0m)]);

    /// <summary>Impuesto de una ganancia. Una pérdida no paga.</summary>
    public Money On(Money gain)
    {
        if (gain.Amount <= 0m)
        {
            return Money.Euros(0m);
        }

        var remaining = gain.Amount;
        var previous = 0m;
        var tax = 0m;

        foreach (var (upTo, rate) in Brackets)
        {
            var slice = Math.Min(remaining, upTo - previous);

            tax += slice * rate;
            remaining -= slice;
            previous = upTo;

            if (remaining <= 0m)
            {
                break;
            }
        }

        return Money.Euros(tax);
    }
}

/// <summary>Una operación simulada.</summary>
public sealed record SimulatedTrade(
    DateOnly EntryDate,
    Money EntryPrice,
    DateOnly? ExitDate,
    Money? ExitPrice,
    Quantity Quantity,
    Money Cost,
    Money Proceeds,
    Money Fees,
    string EntryReason,
    string? ExitReason)
{
    /// <summary>Resultado antes de impuestos. Las comisiones ya están dentro.</summary>
    public Money Result => Proceeds - Cost;

    public bool IsClosed => ExitDate is not null;
}

/// <summary>Lo que deja una simulación.</summary>
/// <param name="BuyAndHold">Lo que habría dado comprar el primer día y no tocar nada.</param>
public sealed record BacktestResult(
    IReadOnlyList<SimulatedTrade> Trades,
    Money Result,
    Money ResultAfterTax,
    Money Fees,
    Money Tax,
    Money BuyAndHold,
    decimal MaximumDrawdown)
{
    public int Closed => Trades.Count(trade => trade.IsClosed);

    public int Winners => Trades.Count(trade => trade.IsClosed && trade.Result.Amount > 0m);

    /// <summary>Si el sistema no bate a no hacer nada, el sistema sobra.</summary>
    public bool BeatsBuyAndHold => Result.Amount > BuyAndHold.Amount;
}

/// <summary>
/// Simula un sistema sobre el histórico.
/// </summary>
/// <remarks>
/// Usa el mismo motor que emite las señales de hoy: dos motores distintos divergen, y
/// entonces la simulación deja de decir nada sobre lo que pasará.
///
/// Cada operación se ejecuta al cierre del día siguiente a su señal. La señal nace del
/// cierre, así que comprar a ese mismo cierre es comprar a un precio que ya no existía
/// cuando se supo. Es el error que más infla un simulador.
/// </remarks>
public static class Backtest
{
    public static BacktestResult Run(
        Guid assetId,
        IReadOnlyList<PricePoint> series,
        StrategyVersion version,
        Money capital,
        TradingCost cost,
        SavingsTax tax)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(cost);
        ArgumentNullException.ThrowIfNull(tax);

        var signals = SignalEngine.Run(assetId, series, version);
        var byDate = series.ToDictionary(point => point.Date, point => point.PriceInEuros);
        var dates = series.Select(point => point.Date).ToList();

        var trades = new List<SimulatedTrade>();
        SimulatedTrade? open = null;

        foreach (var signal in signals)
        {
            // Al día siguiente: es cuando se pudo operar de verdad.
            if (Next(dates, signal.Date) is not { } executed || !byDate.TryGetValue(executed, out var price))
            {
                continue;
            }

            if (signal.Direction == SignalDirection.Entry && open is null)
            {
                var fee = cost.On(capital);
                var invested = capital - fee;
                var quantity = new Quantity(invested.Amount / price);

                open = new SimulatedTrade(
                    executed,
                    Money.Euros(price),
                    null,
                    null,
                    quantity,
                    capital,
                    Money.Euros(0m),
                    fee,
                    signal.Reason,
                    null);

                continue;
            }

            if (signal.Direction == SignalDirection.Exit && open is { } position)
            {
                var gross = Money.Euros(position.Quantity.Value * price);
                var fee = cost.On(gross);

                trades.Add(position with
                {
                    ExitDate = executed,
                    ExitPrice = Money.Euros(price),
                    Proceeds = gross - fee,
                    Fees = position.Fees + fee,
                    ExitReason = signal.Reason,
                });

                open = null;
            }
        }

        if (open is { } pending)
        {
            // La posición sigue abierta al final del periodo: se valora al último cierre
            // sin cobrar la comisión de venta, que no se ha pagado.
            var last = Money.Euros(pending.Quantity.Value * series[^1].PriceInEuros);

            trades.Add(pending with { Proceeds = last });
        }

        return Result(trades, series, capital, cost, tax);
    }

    private static BacktestResult Result(
        List<SimulatedTrade> trades,
        IReadOnlyList<PricePoint> series,
        Money capital,
        TradingCost cost,
        SavingsTax tax)
    {
        var result = trades.Aggregate(Money.Euros(0m), (total, trade) => total + trade.Result);
        var fees = trades.Aggregate(Money.Euros(0m), (total, trade) => total + trade.Fees);
        var taxes = trades
            .Where(trade => trade.IsClosed)
            .Aggregate(Money.Euros(0m), (total, trade) => total + tax.On(trade.Result));

        return new BacktestResult(
            trades,
            result,
            result - taxes,
            fees,
            taxes,
            BuyAndHold(series, capital, cost),
            Drawdown(trades));
    }

    /// <summary>Comprar el primer día y no tocar nada, con su comisión de compra.</summary>
    private static Money BuyAndHold(IReadOnlyList<PricePoint> series, Money capital, TradingCost cost)
    {
        if (series.Count < 2 || series[0].PriceInEuros <= 0m)
        {
            return Money.Euros(0m);
        }

        var invested = capital - cost.On(capital);
        var quantity = invested.Amount / series[0].PriceInEuros;

        return Money.Euros(quantity * series[^1].PriceInEuros) - capital;
    }

    /// <summary>Mayor caída del resultado acumulado operación a operación.</summary>
    private static decimal Drawdown(List<SimulatedTrade> trades)
    {
        var running = 0m;
        var peak = 0m;
        var worst = 0m;

        foreach (var trade in trades.Where(trade => trade.IsClosed))
        {
            running += trade.Result.Amount;
            peak = Math.Max(peak, running);

            if (peak > 0m)
            {
                worst = Math.Max(worst, (peak - running) / peak);
            }
        }

        return worst;
    }

    private static DateOnly? Next(List<DateOnly> dates, DateOnly after)
    {
        var index = dates.IndexOf(after);

        return index >= 0 && index + 1 < dates.Count ? dates[index + 1] : null;
    }
}
