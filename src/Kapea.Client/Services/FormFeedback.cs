using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Kapea.Client.Services;

/// <summary>Cómo se avisa de un formulario incompleto.</summary>
public static class FormFeedback
{
    /// <summary>
    /// Dice qué falta por rellenar.
    /// </summary>
    /// <remarks>
    /// Un diálogo que se cierra sin hacer nada y sin decir por qué es indistinguible de
    /// uno que ha funcionado: se ve que el resultado no aparece, pero no qué faltaba.
    /// </remarks>
    public static void ShowMissing(
        this IToastService toasts,
        IStringLocalizer strings,
        IReadOnlyList<string> missing)
    {
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(missing);

        if (missing.Count == 0)
        {
            return;
        }

        var names = string.Join(", ", missing.Select(key => strings[key].Value));

        toasts.ShowWarning(string.Format(CultureInfo.CurrentCulture, strings["Form_Missing"].Value, names));
    }
}
