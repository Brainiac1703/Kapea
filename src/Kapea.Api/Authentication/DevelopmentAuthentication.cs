using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kapea.Api.Authentication;

/// <summary>
/// Autenticación de conveniencia para desarrollo local.
/// </summary>
/// <remarks>
/// Existe para que el entorno de docker compose se pueda levantar sin un inquilino de
/// Entra. No es un modo relajado del real: es un esquema distinto que solo se registra
/// cuando el entorno es Development y no hay autoridad configurada, y el arranque
/// falla si alguna vez se dieran las dos cosas fuera de desarrollo.
///
/// El identificador es fijo y configurable para que los datos importados sobrevivan a
/// un reinicio del contenedor; uno aleatorio dejaría la cartera vacía en cada arranque.
/// </remarks>
public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<DevelopmentUserOptions> user) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("oid", user.Value.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Value.DisplayName),
            ],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Usuario con el que se trabaja en desarrollo cuando no hay Entra configurado.</summary>
public sealed class DevelopmentUserOptions
{
    public const string SectionName = "Authentication:DevelopmentUser";

    /// <summary>Fijo a propósito: los datos importados tienen que sobrevivir a un reinicio.</summary>
    public Guid Id { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public string DisplayName { get; set; } = "Usuario de desarrollo";
}
