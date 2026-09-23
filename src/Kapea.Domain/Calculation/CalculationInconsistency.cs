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
/// <remarks>
/// Es una clase con constructor privado sin parámetros, como el resto de la proyección,
/// porque EF Core no sabe enlazar tipos complejos a parámetros de constructor.
///
/// No lleva descripción: el texto que lee el usuario se compone donde se muestra, con
/// sus recursos de idioma. Guardar aquí una frase la habría dejado sin traducir y
/// congelada en el momento del cálculo.
/// </remarks>
public sealed class CalculationInconsistency
{
    private CalculationInconsistency()
    {
    }

    public CalculationInconsistency(
        UserId userId,
        InconsistencyKind kind,
        Guid assetId,
        Guid transactionId,
        Occurrence occurredAt,
        Quantity missingQuantity)
    {
        UserId = userId;
        Kind = kind;
        AssetId = assetId;
        TransactionId = transactionId;
        OccurredAt = occurredAt;
        MissingQuantity = missingQuantity;
    }

    public UserId UserId { get; private set; }

    public InconsistencyKind Kind { get; private set; }

    public Guid AssetId { get; private set; }

    /// <summary>Movimiento que no se ha podido calcular.</summary>
    public Guid TransactionId { get; private set; }

    public Occurrence OccurredAt { get; private set; }

    /// <summary>Lo que falta para poder calcularlo. Cero cuando lo que falta no es una cantidad.</summary>
    public Quantity MissingQuantity { get; private set; }
}
