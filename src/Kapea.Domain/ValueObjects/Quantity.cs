using System.Globalization;
using Kapea.Domain.Common;

namespace Kapea.Domain.ValueObjects;

/// <summary>
/// Cantidad de un activo. Es un tipo propio y no un decimal suelto para que no se
/// pueda restar una cantidad de un importe, y para centralizar la comprobación de
/// que nunca queda negativa.
/// </summary>
/// <remarks>
/// decimal ofrece 28-29 dígitos significativos, holgado para los 8 decimales de
/// bitcoin y para los 18 de un token ERC-20.
/// </remarks>
public readonly record struct Quantity : IComparable<Quantity>
{
    public static readonly Quantity Zero = new(0m);

    public Quantity(decimal value)
    {
        if (value < 0m)
        {
            throw new DomainException($"Una cantidad no puede ser negativa: {value}.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public bool IsZero => Value == 0m;

    public static Quantity operator +(Quantity left, Quantity right) => new(left.Value + right.Value);

    public static Quantity operator -(Quantity left, Quantity right)
    {
        if (right.Value > left.Value)
        {
            throw new DomainException(
                $"Una resta de cantidades no puede quedar negativa: {left.Value} - {right.Value}.");
        }

        return new Quantity(left.Value - right.Value);
    }

    public static Quantity operator *(Quantity left, decimal factor) => new(left.Value * factor);

    public static Money operator *(Quantity quantity, Money unitPrice) => unitPrice * quantity.Value;

    public static bool operator <(Quantity left, Quantity right) => left.Value < right.Value;

    public static bool operator >(Quantity left, Quantity right) => left.Value > right.Value;

    public static bool operator <=(Quantity left, Quantity right) => left.Value <= right.Value;

    public static bool operator >=(Quantity left, Quantity right) => left.Value >= right.Value;

    public Quantity Add(Quantity other) => this + other;

    public Quantity Subtract(Quantity other) => this - other;

    /// <summary>Menor de las dos cantidades, para consumir de un lote sin pasarse de su resto.</summary>
    public static Quantity Min(Quantity left, Quantity right) => left <= right ? left : right;

    public int CompareTo(Quantity other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
