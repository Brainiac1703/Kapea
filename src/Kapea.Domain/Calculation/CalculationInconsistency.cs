using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

public enum InconsistencyKind
{
    /// <summary>Una transmisión pide más cantidad de la que hay en lotes: falta importar alguna adquisición.</summary>
    InsufficientLots = 1,

    /// <summary>Un traspaso interno confirmado quiere mover más cantidad de la que hay en la cuenta de origen.</summary>
    InsufficientLotsForTransfer = 2,

    /// <summary>Un split sin proporción: el dato llegó incompleto y aplicarlo a ciegas alteraría las cantidades.</summary>
    SplitWithoutRatio = 3,
}

/// <summary>
/// Dato que impide calcular una operación. Se acumula y se enseña en lugar de emitir
/// un resultado a medias: una cifra fiscal incompleta que parece completa es peor que
/// la ausencia de cifra.
/// </summary>
public sealed record CalculationInconsistency(
    InconsistencyKind Kind,
    Guid AssetId,
    Guid TransactionId,
    Occurrence OccurredAt,
    Quantity MissingQuantity,
    string Description);
