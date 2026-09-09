using System.Globalization;

namespace Kapea.Client.Services;

/// <summary>
/// Formato de las cifras en pantalla.
/// </summary>
/// <remarks>
/// El redondeo solo ocurre aquí. Los importes viajan y se calculan con toda su
/// precisión: recortarlos antes de presentarlos acumularía desviación operación a
/// operación, que es justo lo que el núcleo evita.
/// </remarks>
public static class Format
{
    /// <summary>
    /// Cantidad de un activo sin ceros de relleno. Una acción se cuenta en enteros y
    /// bitcoin en ocho decimales; enseñar los dieciocho de la columna solo estorba.
    /// </summary>
    public static string Quantity(decimal quantity)
    {
        var rounded = Math.Round(quantity, 8, MidpointRounding.ToEven);

        return rounded.ToString("0.########", CultureInfo.CurrentCulture);
    }

    public static string Euros(decimal amount) => amount.ToString("C2", CultureInfo.CurrentCulture);

    /// <summary>Un importe ausente se dice con el texto que le corresponda, no con un cero.</summary>
    public static string Euros(decimal? amount, string whenMissing) =>
        amount is { } value ? Euros(value) : whenMissing;

    public static string Money(decimal amount, string currency) =>
        string.Create(CultureInfo.CurrentCulture, $"{amount:N2} {currency}");

    /// <summary>Clase CSS del signo. El color acompaña al número, nunca lo sustituye.</summary>
    public static string Sign(decimal? amount) => amount switch
    {
        > 0 => "gain",
        < 0 => "loss",
        _ => string.Empty,
    };
}
