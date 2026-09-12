using Kapea.Domain.Calculation;
using Kapea.Domain.MarketData;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Performance;

/// <summary>
/// Qué habría pasado poniendo el mismo dinero, en las mismas fechas, en la referencia.
/// </summary>
/// <param name="Days">Valor de la referencia día a día.</param>
/// <param name="Contributed">Lo aportado, que es idéntico al de la cartera.</param>
/// <param name="IsComplete">Falso si a la referencia le faltó el precio de algún día.</param>
public sealed record BenchmarkResult(
    IReadOnlyList<PortfolioDay> Days,
    Money Contributed,
    bool IsComplete);

/// <summary>
/// Construye la cartera que habría salido de comprar la referencia con cada aportación.
/// </summary>
/// <remarks>
/// Comparar contra el rendimiento pelado de un índice sería tramposo: el índice no
/// recibió el dinero cuando lo recibió la cartera. Aquí se compra la referencia con cada
/// aportación el día que se hizo, de modo que las dos series viven las mismas entradas y
/// la diferencia es lo que aportaron las decisiones.
///
/// Un día sin precio de la referencia no se rellena: la comparación se marca incompleta,
/// porque una referencia inventada haría parecer buena o mala una gestión por un hueco de
/// datos.
/// </remarks>
public static class BenchmarkComparison
{
    public static BenchmarkResult Of(
        IReadOnlyList<PortfolioDay> portfolio,
        IReadOnlyList<DailyPrice> benchmark)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(benchmark);

        var prices = benchmark.ToDictionary(price => price.Date, price => price.PriceInEuros);
        var days = new List<PortfolioDay>(portfolio.Count);
        var units = 0m;
        var contributed = Money.Euros(0m);
        var complete = true;

        foreach (var day in portfolio)
        {
            contributed += day.NetContributionInEuros;

            if (!prices.TryGetValue(day.Date, out var price) || price <= 0m)
            {
                // Sin precio no se puede ni comprar ni valorar ese día.
                complete = false;
                days.Add(new PortfolioDay(day.Date, [], Money.Euros(0m), day.NetContributionInEuros, false));

                continue;
            }

            units += day.NetContributionInEuros.Amount / price;

            days.Add(new PortfolioDay(day.Date, [], Money.Euros(units * price), day.NetContributionInEuros, true));
        }

        return new BenchmarkResult(days, contributed, complete);
    }
}
