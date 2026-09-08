namespace Kapea.Domain.Transactions;

/// <summary>De dónde sale un movimiento. Un ajuste manual no se confunde nunca con un dato importado.</summary>
public enum TransactionOrigin
{
    Imported = 1,
    ManualAdjustment = 2,
}
