using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Exchange;

/// <summary>
/// Tipo de cambio aplicado a una operación, con la fecha y la fuente de la que salió.
/// </summary>
/// <remarks>
/// Se guarda junto al movimiento y no se vuelve a consultar. Sin esto, recalcular un
/// ejercicio ya presentado podría dar cifras distintas a las declaradas si la fuente
/// hubiera revisado sus datos, y eso no es defendible ante nadie.
/// </remarks>
/// <param name="Currency">Divisa distinta del euro a la que se refiere el tipo.</param>
/// <param name="UnitsPerEuro">Unidades de esa divisa por cada euro, como publica el BCE.</param>
/// <param name="RequestedDate">Fecha de la operación para la que se pidió el tipo.</param>
/// <param name="RateDate">Fecha del tipo realmente aplicado. Difiere si hubo sustitución.</param>
/// <param name="Source">Fuente del dato, para poder rehacer la cifra a mano.</param>
public sealed record ExchangeRate(
    Currency Currency,
    decimal UnitsPerEuro,
    DateOnly RequestedDate,
    DateOnly RateDate,
    string Source)
{
    /// <summary>Fuente de referencia: tipos diarios del Banco Central Europeo, que es lo que admite la AEAT.</summary>
    public const string EuropeanCentralBank = "ECB";

    /// <summary>
    /// El tipo aplicado no es el del día de la operación porque ese día no hubo
    /// publicación (fin de semana o festivo). Queda constancia para que la
    /// sustitución sea visible en el detalle del movimiento.
    /// </summary>
    public bool WasSubstituted => RateDate != RequestedDate;

    public static ExchangeRate Create(Currency currency, decimal unitsPerEuro, DateOnly requestedDate, DateOnly rateDate, string source)
    {
        if (currency.IsEuro)
        {
            throw new DomainException("El euro no necesita tipo de cambio contra sí mismo.");
        }

        if (unitsPerEuro <= 0m)
        {
            throw new DomainException($"Un tipo de cambio tiene que ser positivo: {unitsPerEuro}.");
        }

        if (rateDate > requestedDate)
        {
            throw new DomainException(
                "El tipo aplicado no puede ser posterior a la operación: sería usar información que aún no existía.");
        }

        return new ExchangeRate(currency, unitsPerEuro, requestedDate, rateDate, source);
    }

    /// <summary>Convierte a euros un importe expresado en la divisa de este tipo.</summary>
    public Money ToEuros(Money amount)
    {
        if (amount.Currency.IsEuro)
        {
            return amount;
        }

        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(amount.Currency, Currency, "convertir");
        }

        return Money.Euros(amount.Amount / UnitsPerEuro);
    }
}
