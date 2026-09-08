using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Rendimiento del capital: dividendos, intereses y recompensas. No toca los lotes
/// del activo —cobrar un dividendo no cambia lo que costó la acción— y se guarda con
/// el bruto y la retención por separado porque la declaración necesita ambos.
/// </summary>
public sealed record CapitalIncome(
    Guid? AssetId,
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    Occurrence ReceivedAt,
    Money GrossAmountInEuros,
    Money WithholdingInEuros)
{
    public Money NetAmountInEuros => GrossAmountInEuros - WithholdingInEuros;

    public int TaxYear => ReceivedAt.InSourceTimeZone.Year;
}
