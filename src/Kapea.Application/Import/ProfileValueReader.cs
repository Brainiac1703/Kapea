using System.Globalization;
using Kapea.Domain.ImportProfiles;

namespace Kapea.Application.Import;

/// <summary>Una celda no se puede interpretar con las reglas del perfil.</summary>
public sealed class ProfileValueException(string message) : InvalidOperationException(message);

/// <summary>
/// Convierte celdas de texto según las convenciones que declara una versión de perfil.
/// </summary>
/// <remarks>
/// Nada se adivina. Si el perfil dice que los números son europeos, «1.234,56» son mil
/// doscientos treinta y cuatro con cincuenta y seis y «1,234.56» no se lee. Adivinar
/// aquí produciría cifras plausibles y equivocadas, que es el peor resultado posible en
/// algo que acaba en una declaración.
/// </remarks>
public sealed class ProfileValueReader(ImportProfileVersion version)
{
    private readonly CultureInfo _numbers = version.DecimalConvention == DecimalConvention.European
        ? CultureInfo.GetCultureInfo("es-ES")
        : CultureInfo.InvariantCulture;

    public decimal Decimal(string? value, string field)
    {
        if (TryDecimal(value) is { } number)
        {
            return number;
        }

        throw new ProfileValueException(
            $"'{value}' no es un número que se pueda leer como {field} con la convención {Convention()}.");
    }

    public decimal? OptionalDecimal(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : TryDecimal(value);

    public decimal? TryDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // El espacio de millares y el de no separación aparecen en exportaciones reales
        // y no los reconoce ninguna cultura como parte del número.
        var cleaned = value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        const NumberStyles Styles = NumberStyles.Number | NumberStyles.AllowLeadingSign;

        return decimal.TryParse(cleaned, Styles, _numbers, out var number) ? number : null;
    }

    public DateTime Date(string? value)
    {
        if (TryDate(value) is { } date)
        {
            return date;
        }

        throw new ProfileValueException(
            $"'{value}' no encaja con ninguno de los formatos de fecha del perfil ({string.Join(", ", version.DateFormats)}).");
    }

    public DateTime? TryDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        foreach (var format in version.DateFormats)
        {
            if (DateTime.TryParseExact(
                    text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private string Convention() =>
        version.DecimalConvention == DecimalConvention.European ? "europea" : "invariante";
}
