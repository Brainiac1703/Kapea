namespace Kapea.Domain.Transactions;

/// <summary>
/// Tipos de movimiento normalizados. Unknown no es un hueco del modelo: es el destino
/// explícito de lo que el origen trae y no sabemos clasificar, y queda fuera del
/// cálculo hasta que una persona lo resuelve.
/// </summary>
public enum TransactionType
{
    Unknown = 0,
    Buy = 1,
    Sell = 2,
    Deposit = 3,
    Withdrawal = 4,
    Transfer = 5,
    Dividend = 6,
    Fee = 7,
    Interest = 8,
    Reward = 9,
    Split = 10,
}
