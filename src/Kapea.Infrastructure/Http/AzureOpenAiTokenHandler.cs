extern alias AzureIdentity;

using Azure.Core;
using System.Net.Http.Headers;

namespace Kapea.Infrastructure.Http;

/// <summary>
/// Autentica las llamadas al servicio de modelos con la identidad del proceso.
/// </summary>
/// <remarks>
/// La alternativa es la clave del servicio, y en la aplicación desplegada eso
/// significaría una clave viajando a la configuración del contenedor. Con identidad no
/// hay ninguna clave: el token se pide al arrancar la llamada y caduca solo.
///
/// En una máquina de desarrollo no hay identidad administrada, y por eso la clave sigue
/// siendo válida: cuando está configurada, este manejador no se registra.
/// </remarks>
public sealed class AzureOpenAiTokenHandler(TokenCredential credential) : DelegatingHandler
{
    /// <summary>Ámbito del plano de datos de los servicios cognitivos.</summary>
    private static readonly string[] Scopes = ["https://cognitiveservices.azure.com/.default"];

    /// <summary>
    /// Token en curso.
    /// </summary>
    /// <remarks>
    /// La credencial ya guarda el token que obtiene, pero pedirlo en cada llamada añade
    /// una comprobación de caducidad y un cerrojo por petición. Guardarlo aquí deja la
    /// ruta habitual en una lectura de campo.
    /// </remarks>
    private AccessToken _token;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Un minuto de margen: un token que caduca mientras la petición viaja se
        // rechazaría en el servidor, y el reintento costaría más que renovarlo antes.
        if (_token.ExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            _token = await credential
                .GetTokenAsync(new TokenRequestContext(Scopes), cancellationToken)
                .ConfigureAwait(false);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token.Token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>La credencial que se usa cuando nadie inyecta otra.</summary>
    public static TokenCredential DefaultCredential { get; } =
        new AzureIdentity::Azure.Identity.DefaultAzureCredential();
}
