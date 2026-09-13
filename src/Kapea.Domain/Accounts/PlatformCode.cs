using System.Diagnostics.CodeAnalysis;
using Kapea.Domain.Common;

namespace Kapea.Domain.Accounts;

/// <summary>
/// Código con el que se nombra una plataforma.
/// </summary>
/// <remarks>
/// Es texto y no un identificador generado porque viaja dentro de la huella de
/// deduplicación de cada movimiento importado. Un identificador nuevo cambiaría todas
/// las huellas del histórico y la siguiente importación duplicaría lo ya guardado.
///
/// Se compara sin distinguir mayúsculas, pero conserva la forma con la que se dio de
/// alta: es lo que se lee en la interfaz.
/// </remarks>
public readonly record struct PlatformCode : IComparable<PlatformCode>
{
    public const int MaxLength = 16;

    public PlatformCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Una plataforma necesita un código que la identifique.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new DomainException(
                $"El código de plataforma '{trimmed}' pasa de {MaxLength} caracteres.");
        }

        if (!trimmed.All(character => char.IsAsciiLetterOrDigit(character)))
        {
            // Va en la huella de deduplicación y en la clave del secreto: cualquier
            // separador ahí podría hacer que dos plataformas distintas produjeran la
            // misma cadena.
            throw new DomainException(
                $"El código de plataforma '{trimmed}' solo admite letras y dígitos sin acentos.");
        }

        Value = trimmed;
    }

    public string Value { get; }

    public static PlatformCode Xtb { get; } = new("Xtb");

    public static PlatformCode Kraken { get; } = new("Kraken");

    public static PlatformCode Bit2Me { get; } = new("Bit2Me");

    public bool Equals(PlatformCode other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        Value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public int CompareTo(PlatformCode other) =>
        string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Value;

    public static bool TryParse(string? value, [NotNullWhen(true)] out PlatformCode? code)
    {
        try
        {
            code = new PlatformCode(value!);

            return true;
        }
        catch (DomainException)
        {
            code = null;

            return false;
        }
    }
}
