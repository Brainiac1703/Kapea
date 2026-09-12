using Kapea.Domain.Calculation;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Performance;

/// <summary>
/// Rendimiento y riesgo de un periodo.
/// </summary>
/// <param name="TimeWeighted">
/// Rentabilidad ponderada por tiempo: juzga las decisiones, no el tamaño de las
/// aportaciones.
/// </param>
/// <param name="MoneyWeighted">
/// Rentabilidad ponderada por dinero del mismo periodo: juzga el resultado, teniendo en
/// cuenta cuándo entró cada euro. Nula cuando no se puede resolver.
/// </param>
/// <param name="Volatility">Desviación típica de los rendimientos diarios, anualizada.</param>
/// <param name="MaximumDrawdown">Mayor caída desde un máximo anterior, en tanto por uno.</param>
/// <param name="DrawdownRecoveredInDays">
/// Días que tardó en recuperarse esa caída, o nada si sigue abierta.
/// </param>
/// <param name="IsComplete">Falso si el periodo tenía días sin precio.</param>
public sealed record PerformanceResult(
    decimal TimeWeighted,
    decimal? MoneyWeighted,
    decimal Volatility,
    decimal MaximumDrawdown,
    int? DrawdownRecoveredInDays,
    bool IsComplete);

/// <summary>
/// Mide la cartera como se mide un fondo.
/// </summary>
/// <remarks>
/// Restar coste a valor no dice nada cuando el dinero entra a plazos: una cartera que
/// recibe una aportación grande parece haber ganado sin que nada haya subido. Por eso hay
/// dos rentabilidades y no una, y por eso las dos se calculan aquí, como función pura de
/// la serie diaria.
/// </remarks>
public static class PortfolioPerformance
{
    /// <summary>Días hábiles que se usan para anualizar la volatilidad de una serie diaria.</summary>
    public const int TradingDaysPerYear = 365;

    public static PerformanceResult Of(IReadOnlyList<PortfolioDay> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        var complete = days.All(day => day.IsComplete);

        // Un día al que le falta un precio no es un día en que la cartera valiera cero.
        // Contarlo hundiría la cadena a menos cien por cien y dejaría una caída máxima
        // del cien por cien, que es lo que pasaba antes de apartarlos.
        var usable = Usable(days);

        if (usable.Count < 2)
        {
            return new PerformanceResult(0m, null, 0m, 0m, null, complete);
        }

        var daily = DailyReturns(usable);

        var (drawdown, recovered) = Drawdown(usable);

        return new PerformanceResult(
            Chain(daily),
            MoneyWeighted(usable),
            Volatility(daily),
            drawdown,
            recovered,
            complete);
    }

    /// <summary>
    /// Los días cuyo valor se puede usar, arrastrando a ellos lo aportado en los que no.
    /// </summary>
    /// <remarks>
    /// Si la aportación de un día apartado se perdiera, el día siguiente parecería haber
    /// ganado ese dinero. Se acumula y se suma al primer día que sí sirve.
    /// </remarks>
    private static List<PortfolioDay> Usable(IReadOnlyList<PortfolioDay> days)
    {
        var usable = new List<PortfolioDay>(days.Count);
        var pending = Money.Euros(0m);

        foreach (var day in days)
        {
            if (!day.IsComplete)
            {
                pending += day.NetContributionInEuros;

                continue;
            }

            usable.Add(day with { NetContributionInEuros = day.NetContributionInEuros + pending });
            pending = Money.Euros(0m);
        }

        return usable;
    }

    /// <summary>
    /// Rendimiento de cada día contra el anterior, descontando lo aportado ese día.
    /// </summary>
    /// <remarks>
    /// Sin descontar la aportación, meter mil euros en una cartera de mil parecería un
    /// cien por cien de rentabilidad.
    /// </remarks>
    internal static IReadOnlyList<decimal> DailyReturns(IReadOnlyList<PortfolioDay> days)
    {
        var returns = new List<decimal>(days.Count);

        for (var index = 1; index < days.Count; index++)
        {
            var opening = days[index - 1].ValueInEuros.Amount + days[index].NetContributionInEuros.Amount;

            // Sin nada invertido al empezar el día no hay rentabilidad que medir: lo que
            // entre ese día no ha tenido tiempo de rendir.
            returns.Add(opening == 0m ? 0m : (days[index].ValueInEuros.Amount - opening) / opening);
        }

        return returns;
    }

    /// <summary>Encadena los rendimientos diarios, que es lo que neutraliza las aportaciones.</summary>
    private static decimal Chain(IReadOnlyList<decimal> daily)
    {
        var accumulated = 1m;

        foreach (var day in daily)
        {
            accumulated *= 1m + day;
        }

        return accumulated - 1m;
    }

    private static decimal Volatility(IReadOnlyList<decimal> daily)
    {
        if (daily.Count < 2)
        {
            return 0m;
        }

        var average = daily.Average();
        var variance = daily.Sum(value => (value - average) * (value - average)) / (daily.Count - 1);

        return (decimal)Math.Sqrt((double)variance * TradingDaysPerYear);
    }

    /// <summary>
    /// La mayor caída desde un máximo anterior y lo que tardó en recuperarse.
    /// </summary>
    /// <remarks>
    /// Es la cifra que dice cuánto había que aguantar, y la que más se parece a lo que
    /// una persona recuerda de un mal año.
    /// </remarks>
    private static (decimal Drawdown, int? RecoveredInDays) Drawdown(IReadOnlyList<PortfolioDay> days)
    {
        var peak = 0m;
        var peakAt = 0;
        var worst = 0m;
        var worstPeakAt = 0;
        var worstAt = 0;

        for (var index = 0; index < days.Count; index++)
        {
            var value = days[index].ValueInEuros.Amount;

            if (value > peak)
            {
                peak = value;
                peakAt = index;

                continue;
            }

            if (peak == 0m)
            {
                continue;
            }

            var fall = (peak - value) / peak;

            if (fall > worst)
            {
                worst = fall;
                worstPeakAt = peakAt;
                worstAt = index;
            }
        }

        if (worst == 0m)
        {
            return (0m, null);
        }

        var recovery = days
            .Select((day, index) => (day, index))
            .Skip(worstAt)
            .FirstOrDefault(entry => entry.day.ValueInEuros.Amount >= days[worstPeakAt].ValueInEuros.Amount);

        return (worst, recovery.day is null ? null : recovery.index - worstPeakAt);
    }

    /// <summary>
    /// La tasa que iguala las aportaciones con el valor final.
    /// </summary>
    /// <remarks>
    /// Se busca por bisección en lugar de resolverse: la ecuación no tiene solución
    /// cerrada con aportaciones irregulares, y a esta escala la búsqueda converge en unas
    /// decenas de pasos. Nula cuando no hay nada aportado, porque entonces no hay tasa
    /// que buscar.
    ///
    /// La tasa es del periodo y no anual, para que se pueda comparar con la ponderada
    /// por tiempo. Anualizar unos días convierte un veinte por ciento en tres días en una
    /// cifra de varios miles que no significa nada.
    /// </remarks>
    private static decimal? MoneyWeighted(IReadOnlyList<PortfolioDay> days)
    {
        var flows = new List<(int Day, decimal Amount)>();

        for (var index = 0; index < days.Count; index++)
        {
            if (days[index].NetContributionInEuros.Amount != 0m)
            {
                flows.Add((index, -days[index].NetContributionInEuros.Amount));
            }
        }

        // Lo que ya había antes de empezar cuenta como aportación del primer día, y el
        // valor final como el cobro que cierra la serie. Se descuenta lo aportado ese
        // mismo día, que ya está en la lista: contarlo dos veces hundiría la tasa.
        var opening = days[0].ValueInEuros.Amount - days[0].NetContributionInEuros.Amount;

        if (opening != 0m)
        {
            flows.Insert(0, (0, -opening));
        }

        if (flows.Count == 0 || days[^1].ValueInEuros.Amount == 0m)
        {
            return null;
        }

        flows.Add((days.Count - 1, days[^1].ValueInEuros.Amount));

        return Solve(flows, days.Count);
    }

    private static decimal? Solve(List<(int Day, decimal Amount)> flows, int span)
    {
        var low = -0.9999m;
        var high = 1000m;

        if (Math.Sign(Value(flows, low, span)) == Math.Sign(Value(flows, high, span)))
        {
            return null;
        }

        for (var step = 0; step < 128; step++)
        {
            var middle = (low + high) / 2m;

            if (Math.Sign(Value(flows, middle, span)) == Math.Sign(Value(flows, low, span)))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return decimal.Round((low + high) / 2m, 6);
    }

    /// <summary>Valor actual de los flujos a una tasa anual dada.</summary>
    private static decimal Value(List<(int Day, decimal Amount)> flows, decimal rate, int span)
    {
        var total = 0m;

        foreach (var (day, amount) in flows)
        {
            // Proporción del periodo que le queda por delante a ese flujo.
            var share = span <= 1 ? 0d : (double)(span - 1 - day) / (span - 1);

            total += amount * (decimal)Math.Pow(1d + (double)rate, share);
        }

        return total;
    }
}
