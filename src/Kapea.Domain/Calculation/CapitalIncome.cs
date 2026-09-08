using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Rendimiento del capital: dividendos, intereses y recompensas. No toca los lotes
/// del activo —cobrar un dividendo no cambia lo que costó la acción— y se guarda con
/// el bruto y la retención por separado porque la declaración necesita ambos.
/// </summary>
public sealed class CapitalIncome
{
    private CapitalIncome()
    {
    }

    public CapitalIncome(
        UserId userId,
        Guid? assetId,
        Guid transactionId,
        Guid accountId,
        TransactionType type,
        Occurrence receivedAt,
        Money grossAmountInEuros,
        Money withholdingInEuros)
    {
        UserId = userId;
        AssetId = assetId;
        TransactionId = transactionId;
        AccountId = accountId;
        Type = type;
        ReceivedAt = receivedAt;
        GrossAmountInEuros = grossAmountInEuros;
        WithholdingInEuros = withholdingInEuros;
    }

    public UserId UserId { get; private set; }

    public Guid? AssetId { get; private set; }

    public Guid TransactionId { get; private set; }

    public Guid AccountId { get; private set; }

    public TransactionType Type { get; private set; }

    public Occurrence ReceivedAt { get; private set; }

    public Money GrossAmountInEuros { get; private set; }

    public Money WithholdingInEuros { get; private set; }

    public Money NetAmountInEuros => GrossAmountInEuros - WithholdingInEuros;

    public int TaxYear => ReceivedAt.InSourceTimeZone.Year;
}
