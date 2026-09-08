using Kapea.Domain.Lots;

namespace Kapea.Domain.Calculation;

/// <summary>Salida del motor para un activo: la proyección completa que sustituye a la anterior.</summary>
public sealed record AssetCalculationResult(
    Guid AssetId,
    IReadOnlyList<Lot> Lots,
    IReadOnlyList<RealizedResult> RealizedResults,
    IReadOnlyList<CapitalIncome> CapitalIncomes,
    IReadOnlyList<CalculationInconsistency> Inconsistencies,
    int UnresolvedTransactionCount)
{
    /// <summary>Lotes con cantidad pendiente. Son los que forman la posición abierta.</summary>
    public IReadOnlyList<Lot> OpenLots => [.. Lots.Where(lot => !lot.IsExhausted)];

    /// <summary>
    /// Mientras haya movimientos sin clasificar o inconsistencias, las cifras están
    /// incompletas y quien las lea tiene que saberlo.
    /// </summary>
    public bool IsComplete => UnresolvedTransactionCount == 0 && Inconsistencies.Count == 0;
}
