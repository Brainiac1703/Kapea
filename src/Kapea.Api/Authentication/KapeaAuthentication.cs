using System.Security.Claims;
using Kapea.Application.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace Kapea.Api.Authentication;

/// <summary>Proveedores de identidad soportados.</summary>
public static class IdentityProviders
{
    public const string Google = "Google";

    /// <summary>Preparado, no implementado: exige una cuenta de Apple Developer de pago.</summary>
    public const string Apple = "Apple";
}

/// <summary>
/// Configuración de acceso.
/// </summary>
/// <remarks>
/// El intercambio con el proveedor ocurre en el servidor, que es quien puede custodiar
/// el secreto de cliente, y al navegador solo llega una cookie de sesión. Un token del
/// proveedor en el navegador sería un token expuesto, por la misma razón por la que las
/// credenciales de los brókeres tampoco salen de aquí.
/// </remarks>
public static class KapeaAuthentication
{
    public const string SessionScheme = "Kapea";

    /// <summary>Reclamación donde viaja el identificador interno, que no es el del proveedor.</summary>
    public const string UserIdClaim = "kapea:uid";

    public static AuthenticationBuilder AddKapeaAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var google = configuration.GetSection("Authentication:Google");
        var hasGoogle = !string.IsNullOrWhiteSpace(google["ClientId"]);

        if (!hasGoogle && !isDevelopment)
        {
            // Sin esta comprobación, un despliegue al que se le olvidara configurar el
            // proveedor arrancaría con el acceso de desarrollo, abierto a cualquiera.
            throw new InvalidOperationException(
                "Falta Authentication:Google:ClientId. Fuera de desarrollo la API no arranca sin identidad configurada.");
        }

        var builder = services
            .AddAuthentication(hasGoogle ? SessionScheme : DevelopmentAuthenticationHandler.SchemeName)
            .AddCookie(SessionScheme, options =>
            {
                options.Cookie.Name = "kapea.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = isDevelopment
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);

                // Una petición de la API responde 401 en lugar de redirigir al proveedor:
                // el cliente WebAssembly necesita distinguir «no autenticado» de una
                // página de inicio de sesión devuelta como si fuera datos.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    return Task.CompletedTask;
                };
            });

        if (hasGoogle)
        {
            builder.AddGoogle(IdentityProviders.Google, options =>
            {
                options.ClientId = google["ClientId"]!;
                options.ClientSecret = google["ClientSecret"] ?? string.Empty;
                options.SignInScheme = SessionScheme;
                options.SaveTokens = false;
            });
        }
        else
        {
            services.Configure<DevelopmentUserOptions>(
                configuration.GetSection(DevelopmentUserOptions.SectionName));

            builder.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
                DevelopmentAuthenticationHandler.SchemeName, _ => { });
        }

        return builder;
    }

    /// <summary>Traduce lo que devuelve el proveedor al contrato que entiende la capa de aplicación.</summary>
    public static ExternalPrincipal ToExternalPrincipal(this ClaimsPrincipal principal, string provider)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException(
                $"{provider} no ha entregado el identificador de sujeto, que es lo único que identifica a la persona.");

        return new ExternalPrincipal(
            provider,
            subject,
            principal.FindFirstValue(ClaimTypes.Name),
            principal.FindFirstValue(ClaimTypes.Email));
    }
}
