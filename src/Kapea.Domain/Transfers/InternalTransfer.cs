using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Transfers;

/// <summary>Situación de un emparejamiento propuesto.</summary>
public enum InternalTransferStatus
{
    /// <summary>Propuesto por el sistema, pendiente de que una persona decida.</summary>
    Proposed = 1,

    /// <summary>Confirmado: los lotes se trasladan y no hay hecho imponible.</summary>
    Confirmed = 2,

    /// <summary>Rechazado: son dos operaciones independientes y no se vuelve a proponer.</summary>
    Rejected = 3,
}

/// <summary>
/// Traspaso entre dos cuentas del propio usuario.
/// </summary>
/// <remarks>
/// Nunca se aplica solo. Un falso positivo convertiría una venta real en un traspaso y
/// borraría un hecho imponible del cálculo fiscal; un falso negativo solo genera ruido
/// que se revisa. La asimetría del coste es lo que obliga a la confirmación humana.
/// </remarks>
public sealed class InternalTransfer
{
    private InternalTransfer()
    {
    }

    private InternalTransfer(
        Guid id,
        UserId userId,
        Guid outgoingTransactionId,
        Guid incomingTransactionId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        Guid assetId,
        Quantity sentQuantity,
        Quantity receivedQuantity,
        DateTimeOffset proposedAt)
    {
        Id = id;
        UserId = userId;
        OutgoingTransactionId = outgoingTransactionId;
        IncomingTransactionId = incomingTransactionId;
        SourceAccountId = sourceAccountId;
        DestinationAccountId = destinationAccountId;
        AssetId = assetId;
        SentQuantity = sentQuantity;
        ReceivedQuantity = receivedQuantity;
        ProposedAt = proposedAt;
        Status = InternalTransferStatus.Proposed;
    }

    public Guid Id { get; private set; }

    public UserId UserId { get; private set; }

    public Guid OutgoingTransactionId { get; private set; }

    public Guid IncomingTransactionId { get; private set; }

    public Guid SourceAccountId { get; private set; }

    public Guid DestinationAccountId { get; private set; }

    public Guid AssetId { get; private set; }

    public Quantity SentQuantity { get; private set; }

    public Quantity ReceivedQuantity { get; private set; }

    public InternalTransferStatus Status { get; private set; }

    public DateTimeOffset ProposedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// Diferencia entre lo enviado y lo recibido: la comisión de red. No genera
    /// resultado, encarece los lotes que llegan al destino.
    /// </summary>
    public Quantity NetworkFeeQuantity => SentQuantity - ReceivedQuantity;

    public bool IsConfirmed => Status == InternalTransferStatus.Confirmed;

    /// <summary>Mientras no se resuelva, sus dos movimientos quedan fuera del cálculo.</summary>
    public bool IsPending => Status == InternalTransferStatus.Proposed;

    public static InternalTransfer Propose(
        UserId userId,
        Guid outgoingTransactionId,
        Guid incomingTransactionId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        Guid assetId,
        Quantity sentQuantity,
        Quantity receivedQuantity,
        DateTimeOffset proposedAt)
    {
        if (sourceAccountId == destinationAccountId)
        {
            throw new DomainException("Un traspaso interno va entre dos cuentas distintas.");
        }

        if (receivedQuantity > sentQuantity)
        {
            throw new DomainException("En un traspaso no se puede recibir más de lo enviado.");
        }

        return new InternalTransfer(
            Guid.NewGuid(), userId, outgoingTransactionId, incomingTransactionId, sourceAccountId,
            destinationAccountId, assetId, sentQuantity, receivedQuantity, proposedAt);
    }

    public void Confirm(DateTimeOffset resolvedAt)
    {
        EnsurePending();

        Status = InternalTransferStatus.Confirmed;
        ResolvedAt = resolvedAt;
    }

    /// <summary>
    /// El usuario dice que no son el mismo movimiento. Se conserva la decisión para no
    /// volver a proponer el mismo emparejamiento en cada sincronización.
    /// </summary>
    public void Reject(DateTimeOffset resolvedAt)
    {
        EnsurePending();

        Status = InternalTransferStatus.Rejected;
        ResolvedAt = resolvedAt;
    }

    private void EnsurePending()
    {
        if (!IsPending)
        {
            throw new DomainException($"El traspaso ya está {Status} y no se puede volver a resolver.");
        }
    }
}
