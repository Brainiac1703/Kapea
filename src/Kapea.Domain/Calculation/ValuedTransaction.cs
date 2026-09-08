using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Movimiento con sus importes ya convertidos a euros y con lo que el cálculo
/// necesita saber de él. El motor no consulta tipos de cambio ni la base de datos:
/// recibe todo resuelto, y eso es lo que lo hace una función pura y reproducible.
/// </summary>
/// <param name="Transaction">Movimiento de origen.</param>
/// <param name="GrossAmountInEuros">Importe bruto en euros al tipo de la fecha de la operación.</param>
/// <param name="FeeInEuros">Comisión en euros.</param>
/// <param name="WithholdingInEuros">Retención en euros, cuando el origen la aporta.</param>
/// <param name="SplitRatio">Proporción del split. Solo en movimientos de tipo Split.</param>
/// <param name="InternalTransferId">Traspaso interno confirmado al que pertenece el movimiento.</param>
/// <param name="TransferDestinationAccountId">Cuenta de destino del traspaso, en la pata de salida.</param>
/// <param name="IsPendingTransferReview">El movimiento tiene un traspaso propuesto sin resolver.</param>
public sealed record ValuedTransaction(
    Transaction Transaction,
    Money GrossAmountInEuros,
    Money FeeInEuros,
    Money? WithholdingInEuros = null,
    decimal? SplitRatio = null,
    Guid? InternalTransferId = null,
    Guid? TransferDestinationAccountId = null,
    bool IsPendingTransferReview = false)
{
    /// <summary>
    /// Valora el movimiento con el tipo de cambio que lleva congelado. Es la única vía
    /// prevista para entrar al motor: garantiza que un recálculo use el mismo tipo que
    /// el cálculo original, aunque la fuente haya cambiado sus datos desde entonces.
    /// </summary>
    public static ValuedTransaction From(
        Transaction transaction,
        decimal? splitRatio = null,
        Guid? internalTransferId = null,
        Guid? transferDestinationAccountId = null,
        bool isPendingTransferReview = false)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new ValuedTransaction(
            transaction,
            transaction.GrossAmountInEuros,
            transaction.FeeInEuros,
            transaction.WithholdingTaxInEuros,
            splitRatio,
            internalTransferId,
            transferDestinationAccountId,
            isPendingTransferReview);
    }

    public Guid? AssetId => Transaction.AssetId;

    public TransactionType Type => Transaction.Type;

    public Occurrence OccurredAt => Transaction.OccurredAt;

    public Quantity Quantity => Transaction.Quantity;

    /// <summary>
    /// Movimientos que el cálculo no puede tratar todavía. Se apartan en lugar de
    /// suponer nada sobre ellos: una suposición aquí falsea un resultado fiscal.
    /// </summary>
    public bool IsUnresolved => Transaction.RequiresReview || IsPendingTransferReview;

    /// <summary>Pata de salida de un traspaso interno: mueve lotes, no vende.</summary>
    public bool IsInternalTransferOut =>
        InternalTransferId is not null && TransferDestinationAccountId is not null;

    /// <summary>Pata de entrada de un traspaso interno: no crea lote, el lote ya viene de la otra cuenta.</summary>
    public bool IsInternalTransferIn =>
        InternalTransferId is not null && TransferDestinationAccountId is null;

    public void EnsureAmountsAreInEuros()
    {
        if (!GrossAmountInEuros.Currency.IsEuro || !FeeInEuros.Currency.IsEuro)
        {
            throw new DomainException("El motor de cálculo solo acepta importes ya convertidos a euros.");
        }
    }
}
