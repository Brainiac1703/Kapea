using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Calculation;

/// <summary>
/// El dinero que el usuario ha puesto de su bolsillo y el que ha sacado.
/// </summary>
/// <param name="DepositedInEuros">Todo lo ingresado en las plataformas desde el principio.</param>
/// <param name="WithdrawnInEuros">Todo lo retirado de ellas.</param>
/// <param name="MissesAssetsFromOutside">
/// Ha entrado algún activo sin que su dinero pasara por la plataforma, así que lo
/// aportado se queda corto.
/// </param>
public sealed record ContributedCapital(
    Money DepositedInEuros,
    Money WithdrawnInEuros,
    bool MissesAssetsFromOutside)
{
    public static ContributedCapital None { get; } =
        new(Money.Euros(0m), Money.Euros(0m), MissesAssetsFromOutside: false);

    /// <summary>Lo que queda puesto: lo ingresado menos lo retirado.</summary>
    public Money NetInEuros => DepositedInEuros - WithdrawnInEuros;

    /// <summary>Hay algo con lo que comparar el patrimonio.</summary>
    public bool HasContributions => !NetInEuros.IsZero;

    /// <summary>
    /// Cuánto se gana o se pierde sobre lo puesto, comparado con lo que hay hoy.
    /// </summary>
    /// <remarks>
    /// No es la rentabilidad ponderada por dinero: no pondera cuándo entró cada euro.
    /// Dice cuánto se puso y cuánto hay, que es la pregunta que se hace quien mira su
    /// cartera un martes cualquiera.
    /// </remarks>
    public Money ResultAgainst(Money wealthInEuros) => wealthInEuros - NetInEuros;

    /// <summary>
    /// La misma diferencia en tanto por uno sobre lo aportado, o nada si no se aportó.
    /// </summary>
    /// <remarks>
    /// Sin aportaciones no hay porcentaje: dividir entre cero no es cero por ciento, y
    /// un cero ahí se leería como «ni ganas ni pierdes».
    /// </remarks>
    public decimal? ShareAgainst(Money wealthInEuros) => HasContributions
        ? ResultAgainst(wealthInEuros).Amount / NetInEuros.Amount
        : null;
}

/// <summary>
/// Suma el dinero que ha entrado y salido de las plataformas.
/// </summary>
/// <remarks>
/// Sale de los mismos movimientos que el saldo de cada cuenta, y a propósito: si
/// saliera de otro sitio podrían contarse dos historias distintas del mismo dinero.
///
/// Solo cuenta lo que es dinero. Un ingreso que trae un activo es otra cosa —cripto
/// que llega de una cartera de fuera— y no se sabe si fue una compra, un regalo o un
/// cobro, así que no se convierte en aportación: se avisa de que la cifra se queda
/// corta y el usuario decide qué hacer con ello.
/// </remarks>
public static class ContributedCapitalCalculator
{
    /// <summary>
    /// El movimiento es dinero que entra o sale de la plataforma, y por tanto cuenta
    /// como aportación o como retirada.
    /// </summary>
    /// <remarks>
    /// Es la definición única de lo que es aportar, y la comparten el resumen de la
    /// cartera y la serie de evolución. Tenerla en dos sitios acabó dando dos cifras
    /// distintas del mismo dinero, que es lo que se quiere evitar.
    ///
    /// Queda fuera lo que va de una cuenta propia a otra, porque ese dinero ya estaba
    /// dentro, y lo que trae un activo, porque no se sabe si fue una compra, un regalo
    /// o un cobro.
    ///
    /// Un traspaso propuesto y todavía sin resolver no llega hasta aquí: se aparta antes,
    /// con el resto de lo no resuelto. El que sí cuenta es el que nadie ha llegado a
    /// emparejar, porque sin esa pareja no hay forma de distinguirlo de dinero nuevo.
    /// </remarks>
    public static bool IsContribution(ValuedTransaction valued)
    {
        ArgumentNullException.ThrowIfNull(valued);

        return valued.Transaction.Type is TransactionType.Deposit or TransactionType.Withdrawal
            && !valued.IsInternalTransferIn
            && !valued.IsInternalTransferOut
            && valued.AssetId is null;
    }

    public static ContributedCapital Calculate(IEnumerable<ValuedTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var deposited = Money.Euros(0m);
        var withdrawn = Money.Euros(0m);
        var fromOutside = false;

        foreach (var valued in transactions)
        {
            if (valued.IsUnresolved || valued.Transaction.Type is not (TransactionType.Deposit or TransactionType.Withdrawal))
            {
                continue;
            }

            // Un ingreso que trae un activo no es dinero puesto: es cripto que llega de
            // fuera. No suma, pero se declara, porque deja lo aportado corto y la
            // comparación con el patrimonio exagerando la pérdida.
            if (valued.AssetId is not null)
            {
                fromOutside = true;

                continue;
            }

            if (!IsContribution(valued))
            {
                continue;
            }

            if (valued.Transaction.Type == TransactionType.Deposit)
            {
                deposited += valued.GrossAmountInEuros;
            }
            else
            {
                withdrawn += valued.GrossAmountInEuros;
            }
        }

        return new ContributedCapital(deposited, withdrawn, fromOutside);
    }
}
