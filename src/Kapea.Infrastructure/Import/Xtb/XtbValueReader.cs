using System.Globalization;

namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>
/// Interpreta los valores de una celda con las convenciones locales del informe.
/// </summary>
/// <remarks>
/// XTB exporta con convención europea —coma decimal y punto de millares— pero no
/// siempre: según el idioma de la cuenta puede salir con punto decimal. Se prueban
/// ambas y se rechaza lo ambiguo en lugar de adivinar, porque confundir 1.234 con
/// 1,234 son tres órdenes de magnitud.
/// </remarks>
public static class XtbValueReader
{
    private static readonly CultureInfo European = CultureInfo.GetCultureInfo("es-ES");

    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
    ];

    public static bool TryReadDecimal(string? text, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        var hasComma = cleaned.Contains(',', StringComparison.Ordinal);
        var hasDot = cleaned.Contains('.', StringComparison.Ordinal);

        // Con los dos separadores, el último que aparece es el decimal.
        var culture = hasComma && hasDot
            ? (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.') ? European : CultureInfo.InvariantCulture)
            : hasComma ? European : CultureInfo.InvariantCulture;

        return decimal.TryParse(cleaned, NumberStyles.Number, culture, out value);
    }

    public static bool TryReadDate(string? text, out DateTime value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text.Trim();

        if (DateTime.TryParseExact(cleaned, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault, out value))
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified) is var _ && Normalise(ref value);
        }

        return DateTime.TryParse(cleaned, European, DateTimeStyles.AllowWhiteSpaces, out value) && Normalise(ref value);
    }

    private static bool Normalise(ref DateTime value)
    {
        // El motor exige una fecha sin zona explícita, que se interpreta después en la
        // zona que declara el adaptador. Un Kind local aquí sería la zona del servidor.
        value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        return true;
    }
}
