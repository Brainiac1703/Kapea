namespace Kapea.Domain.Transactions;

/// <summary>Hacia dónde mueve la cartera un movimiento.</summary>
public enum MovementDirection
{
    /// <summary>Ni suma ni resta: lo que entra por un lado sale por el otro.</summary>
    Neutral = 0,

    /// <summary>Añade a la cartera: una venta, un ingreso, un rendimiento cobrado.</summary>
    Gain = 1,

    /// <summary>Quita de la cartera: una compra, una retirada, una comisión.</summary>
    Loss = 2,
}

/// <summary>
/// Dice si un movimiento suma o resta, para poder leerlo de un vistazo.
/// </summary>
/// <remarks>
/// No es el efecto en caja, que sólo mira los euros. Una recompensa cobrada en unidades
/// del propio activo no mueve un euro y sin embargo suma: te la regalan, y la cartera
/// vale más que antes sin haber pagado nada.
///
/// Lo que no suma ni resta es el intercambio: una permuta cambia una cosa por otra y un
/// traspaso lleva algo de una cuenta propia a otra. Pintarlos como ganancia o como
/// pérdida diría que pasó algo que no pasó.
/// </remarks>
public static class MovementDirections
{
    public static MovementDirection Of(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        // Una permuta se valora en euros para saber lo que costó, pero es un cambio: sus
        // dos patas juntas no mueven la cartera ni a favor ni en contra.
        if (!transaction.SettledInCash)
        {
            return MovementDirection.Neutral;
        }

        return transaction.Type switch
        {
            TransactionType.Sell
                or TransactionType.Deposit
                or TransactionType.Dividend
                or TransactionType.Interest
                or TransactionType.Reward => MovementDirection.Gain,

            TransactionType.Buy
                or TransactionType.Withdrawal
                or TransactionType.Fee => MovementDirection.Loss,

            // Un traspaso mueve algo de sitio, un split cambia cuántas participaciones
            // hay, y lo que está sin clasificar todavía no se sabe qué es.
            _ => MovementDirection.Neutral,
        };
    }
}
