using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// Lo que cada movimiento deja o quita de la caja de su cuenta, en la divisa de la
/// propia operación.
/// </summary>
/// <remarks>
/// Es función pura del movimiento: el mismo dato da siempre el mismo efecto, sin
/// consultar tipos de cambio ni el resto del histórico. La conversión a euros llega
/// después, y solo para el total.
///
/// El signo lo decide el tipo y no el dato: los importes llegan siempre en positivo
/// porque cada origen firma sus cifras a su manera, y fiarse de ese signo era
/// convertir una retirada en un ingreso según el fichero.
/// </remarks>
public static class CashEffect
{
    /// <summary>
    /// Efecto en caja del movimiento, o null cuando no se puede determinar.
    /// </summary>
    /// <remarks>
    /// Null no es cero. Un traspaso de dinero sin activo no dice hacia dónde va, y
    /// suponerle una dirección deja un saldo creíble y equivocado. Quien suma saldos
    /// cuenta esos movimientos aparte y avisa de que la cifra está incompleta.
    /// </remarks>
    public static Money? Of(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var gross = transaction.GrossAmount;
        var fee = transaction.Fee;
        var nothing = Money.Zero(gross.Currency);

        return transaction.Type switch
        {
            TransactionType.Deposit => gross - fee,
            TransactionType.Withdrawal => -gross - fee,
            TransactionType.Buy => -gross - fee,
            TransactionType.Sell => gross - fee,

            // Un rendimiento cobrado en unidades del propio activo no pasa por caja: lo
            // que entra es un lote, no dinero. Se distingue por la cantidad entregada,
            // que es el mismo criterio con el que el motor decide crear ese lote.
            TransactionType.Dividend or TransactionType.Interest or TransactionType.Reward =>
                transaction.Quantity.IsZero
                    ? gross - fee - (transaction.WithholdingTax ?? nothing)
                    : -fee,

            // La comisión suelta trae su importe como bruto, no como comisión.
            TransactionType.Fee => -gross - fee,

            // Un split cambia cuántas participaciones hay, no el dinero.
            TransactionType.Split => nothing,

            // Mover un activo de una cuenta a otra no es dinero: solo cuesta la comisión
            // de red. Mover dinero sí lo es, pero el movimiento no dice en qué sentido.
            TransactionType.Transfer => transaction.AssetId is not null ? -fee : null,

            _ => null,
        };
    }
}
