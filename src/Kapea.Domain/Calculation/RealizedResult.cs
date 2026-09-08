using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Lote consumido por una transmisión, con la parte del importe y del coste que le
/// corresponde. Es el nivel de detalle que permite defender una cifra ante una gestoría.
/// </summary>
public sealed record ConsumedLot(
    Guid LotId,
    Guid AcquisitionTransactionId,
    Occurrence AcquiredAt,
    Quantity Quantity,
    Money AcquisitionCostInEuros,
    Money ProceedsInEuros)
{
    public Money ResultInEuros => ProceedsInEuros - AcquisitionCostInEuros;
}

/// <summary>
/// Resultado realizado de una transmisión: lo que se ingresó, lo que costó y de qué
/// lotes salió. Se devenga en la fecha de la transmisión, que es la que determina el
/// ejercicio fiscal.
/// </summary>
public sealed record RealizedResult(
    Guid AssetId,
    Guid DisposalTransactionId,
    Guid AccountId,
    Occurrence DisposedAt,
    Quantity Quantity,
    Money ProceedsInEuros,
    Money AcquisitionCostInEuros,
    IReadOnlyList<ConsumedLot> ConsumedLots)
{
    /// <summary>Ganancia o pérdida patrimonial de la transmisión.</summary>
    public Money ResultInEuros => ProceedsInEuros - AcquisitionCostInEuros;

    public int TaxYear => DisposedAt.InSourceTimeZone.Year;
}
