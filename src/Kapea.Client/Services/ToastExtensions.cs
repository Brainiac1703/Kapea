using System.Net;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Kapea.Client.Services;

/// <summary>Cómo se enseña un fallo de la API.</summary>
public static class ToastExtensions
{
    /// <summary>
    /// Enseña el fallo, salvo cuando es que la sesión ya no vale.
    /// </summary>
    /// <remarks>
    /// Ese caso lo resuelve el manejador de caducidad llevando a la pantalla de acceso,
    /// y el aviso solo añadiría un «401» encima de una pantalla que ya explica qué hacer.
    /// </remarks>
    public static void ShowApiError(this IToastService toasts, KapeaApiException exception)
    {
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        // Sin caducidad y con su aspa: un rechazo de la plataforma explica qué hay que
        // corregir, y un aviso que se va solo obliga a repetir la operación solo para
        // volver a leerlo.
        toasts.ShowError(exception.Message, timeout: 0);
    }
}
