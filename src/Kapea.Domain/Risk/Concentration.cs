using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Risk;

/// <summary>Lo que pesa un activo o una clase sobre el total.</summary>
public sealed record Weight(string Name, Money ValueInEuros, decimal Share);

/// <summary>
/// Aviso de que unos pocos activos concentran demasiado.
/// </summary>
/// <param name="Top">Los que más pesan, del mayor al menor.</param>
public sealed record ConcentrationWarning(IReadOnlyList<Weight> Top, decimal Share, decimal Threshold);

/// <summary>Desviación de un peso respecto a su objetivo.</summary>
/// <param name="Adjustment">Lo que habría que comprar o vender para volver al objetivo.</param>
public sealed record RebalanceProposal(string Name, decimal Target, decimal Actual, Money Adjustment);

/// <summary>
/// Mide concentración y desviación de los pesos.
/// </summary>
/// <remarks>
/// No dice si concentrar está bien o mal: eso depende de lo que cada uno quiera. Dice
/// cuánto se está concentrando, que es lo que no se ve mirando una tabla de doce filas.
/// </remarks>
public static class Concentration
{
    /// <summary>Los que más pesan, si juntos superan el umbral.</summary>
    public static ConcentrationWarning? Check(IReadOnlyList<Weight> weights, int count, decimal threshold)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        if (weights.Count == 0 || threshold <= 0m)
        {
            return null;
        }

        var top = weights.OrderByDescending(weight => weight.Share).Take(count).ToList();
        var share = top.Sum(weight => weight.Share);

        return share > threshold ? new ConcentrationWarning(top, share, threshold) : null;
    }

    /// <summary>
    /// Qué habría que mover para volver a los pesos objetivo.
    /// </summary>
    /// <remarks>
    /// Solo se propone cuando la desviación supera la banda. Rebalancear por cualquier
    /// desviación genera operaciones que solo pagan comisiones.
    /// </remarks>
    public static IReadOnlyList<RebalanceProposal> Rebalance(
        IReadOnlyList<Weight> weights,
        IReadOnlyDictionary<string, decimal> targets,
        decimal band,
        Money total)
    {
        ArgumentNullException.ThrowIfNull(weights);
        ArgumentNullException.ThrowIfNull(targets);

        var proposals = new List<RebalanceProposal>();

        foreach (var weight in weights)
        {
            if (!targets.TryGetValue(weight.Name, out var target))
            {
                continue;
            }

            if (Math.Abs(weight.Share - target) <= band)
            {
                continue;
            }

            var wanted = total.Amount * target;

            proposals.Add(new RebalanceProposal(
                weight.Name, target, weight.Share, Money.Euros(wanted - weight.ValueInEuros.Amount)));
        }

        return proposals;
    }
}
