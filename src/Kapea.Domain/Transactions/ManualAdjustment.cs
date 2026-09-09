using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Transactions;

/// <summary>
/// Corrección introducida por el usuario. Existe porque un movimiento importado no
/// se edita: la corrección es un apunte nuevo, con su motivo y su rastro, y no una
/// alteración del dato original.
/// </summary>
public sealed class ManualAdjustment
{
    private ManualAdjustment(Guid id, UserId userId, Guid accountId, string reason, DateTimeOffset createdAt, Transaction transaction)
    {
        Id = id;
        UserId = userId;
        AccountId = accountId;
        Reason = reason;
        CreatedAt = createdAt;
        Transaction = transaction;
    }

    public Guid Id { get; }

    public UserId UserId { get; }

    public Guid AccountId { get; }

    public string Reason { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Movimiento que genera el ajuste. Entra en el cálculo como cualquier otro.</summary>
    public Transaction Transaction { get; }

    public static ManualAdjustment Create(
        UserId userId,
        Guid accountId,
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        Money? unitPrice,
        Money grossAmount,
        Money fee,
        Occurrence occurredAt,
        string reason,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Un ajuste manual necesita motivo: sin él la cifra no es defendible.");
        }

        var id = Guid.NewGuid();

        var transaction = Transaction.FromManualAdjustment(
            userId, accountId, type, assetId, quantity, unitPrice, grossAmount, fee, occurredAt, id, reason);

        return new ManualAdjustment(id, userId, accountId, reason.Trim(), createdAt, transaction);
    }
}
