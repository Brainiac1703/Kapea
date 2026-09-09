using System.Net;
using System.Net.Http.Json;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Services;

/// <summary>
/// Quién tiene la sesión, compartido por toda la aplicación.
/// </summary>
/// <remarks>
/// Se consulta una vez al arrancar y se refresca cuando algo la cambia. Preguntarlo en
/// cada pantalla multiplicaría las llamadas sin cambiar nada de lo que se ve.
///
/// Usa un HttpClient propio y no el cliente de la API: aquel lleva el manejador que
/// redirige al acceso cuando la sesión caduca, y ese manejador necesita este estado.
/// Compartirlos cerraría el círculo y la aplicación no llegaría a arrancar.
/// </remarks>
public sealed class SessionState(HttpClient http)
{
    private bool _loaded;

    public CurrentUserResponse? User { get; private set; }

    public bool IsSignedIn => User is not null;

    public event Action? Changed;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetAsync("api/me", cancellationToken);

            User = response.StatusCode == HttpStatusCode.Unauthorized
                ? null
                : await response.Content.ReadFromJsonAsync<CurrentUserResponse>(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException)
        {
            // Un fallo al preguntar quién eres se trata como no autenticado: es el
            // estado seguro, y la pantalla de acceso siempre es una salida válida.
            User = null;
        }

        _loaded = true;
        Changed?.Invoke();
    }

    public void Clear()
    {
        User = null;
        _loaded = true;
        Changed?.Invoke();
    }
}
