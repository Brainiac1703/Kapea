using Kapea.Domain.Common;

namespace Kapea.Domain.ValueObjects;

/// <summary>
/// Importe monetario con su divisa. El emparejamiento es deliberado: separar importe
/// y divisa permite sumar euros con dólares sin que nada proteste, que es el error
/// clásico de una aplicación multidivisa.
/// </summary>
/// <remarks>
/// El tipo subyacente es decimal en todo el recorrido. Nunca double: la coma flotante
/// binaria no representa 0,1 de forma exacta y el error se acumula al agregar un
/// ejercicio entero.
/// </remarks>
public readonly record struct Money : IComparable<Money>
{
    public Money(decimal amount, Currency currency)
    {
        if (currency == default)
        {
            throw new DomainException("Un importe monetario necesita divisa.");
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money Euros(decimal amount) => new(amount, Currency.Euro);

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "sumar");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right, "restar");

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value) => new(-value.Amount, value.Currency);

    public static Money operator *(Money left, decimal factor) => new(left.Amount * factor, left.Currency);

    public static Money operator *(decimal factor, Money right) => right * factor;

    public static Money operator /(Money left, decimal divisor)
    {
        if (divisor == 0m)
        {
            throw new DomainException("No se puede dividir un importe entre cero.");
        }

        return new Money(left.Amount / divisor, left.Currency);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public Money Add(Money other) => this + other;

    public Money Subtract(Money other) => this - other;

    /// <summary>
    /// Redondea a la unidad mínima de presentación. Solo debe usarse al mostrar o al
    /// emitir un informe: aplicarlo a un valor intermedio introduce una desviación
    /// que se acumula operación a operación.
    /// </summary>
    public Money RoundForDisplay(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.ToEven), Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other, "comparar");

        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => Currency.Format(Amount);

    private static void EnsureSameCurrency(Money left, Money right, string operation)
    {
        if (left.Currency != right.Currency)
        {
            throw new CurrencyMismatchException(left.Currency, right.Currency, operation);
        }
    }
}

/// <summary>Intento de operar con dos importes de divisas distintas sin convertir antes.</summary>
public sealed class CurrencyMismatchException(Currency left, Currency right, string operation)
    : DomainException($"No se pueden {operation} importes en {left.Code} y {right.Code} sin convertir primero.")
{
    public Currency Left { get; } = left;

    public Currency Right { get; } = right;
}
