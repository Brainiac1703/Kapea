using System.Net;
using Microsoft.AspNetCore.Components;

namespace Kapea.Client.Services;

/// <summary>
/// Lleva a la pantalla de acceso cuando la API dice que no hay sesión.
/// </summary>
/// <remarks>
/// Va aquí y no en cada pantalla porque la sesión puede caducar en cualquier llamada.
/// Repartir la comprobación dejaría siempre alguna sin cubrir, y esa se manifestaría
/// como un error técnico en mitad de una operación.
/// </remarks>
public sealed class SessionExpiryHandler(NavigationManager navigation, SessionState session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // La consulta de quién eres se queda fuera: devolver 401 ahí es la respuesta
        // normal de una visita sin sesión, no una sesión que ha caducado.
        if (request.RequestUri?.AbsolutePath.EndsWith("/api/me", StringComparison.Ordinal) == true)
        {
            return response;
        }

        session.Clear();

        var actual = navigation.ToBaseRelativePath(navigation.Uri);

        // Si ya se está en el acceso, no hay a dónde volver: guardarlo como destino
        // anidaría una pantalla de acceso dentro de otra en la dirección.
        if (actual.StartsWith("signin", StringComparison.OrdinalIgnoreCase))
        {
            return response;
        }

        navigation.NavigateTo($"signin?returnUrl=/{Uri.EscapeDataString(actual)}", forceLoad: false);

        return response;
    }
}
