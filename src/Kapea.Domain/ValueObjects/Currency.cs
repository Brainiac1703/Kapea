using System.Globalization;
using Kapea.Domain.Common;

namespace Kapea.Domain.ValueObjects;

/// <summary>
/// Divisa fiduciaria identificada por su código ISO 4217. No representa criptomonedas:
/// esas son activos del catálogo, y confundir ambas cosas es lo que lleva a valorar
/// una permuta como si no lo fuera.
/// </summary>
public readonly record struct Currency
{
    public static readonly Currency Euro = new("EUR");

    private Currency(string code) => Code = code;

    public string Code { get; }

    public static Currency FromCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("El código de divisa no puede estar vacío.");
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"'{code}' no es un código de divisa ISO 4217 válido.");
        }

        return new Currency(normalized);
    }

    public bool IsEuro => Code == Euro.Code;

    public override string ToString() => Code.Length == 0 ? string.Empty : Code;

    internal string Format(decimal amount) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount} {Code}");
}
