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

    /// <summary>Acceso local sin proveedor externo. Solo existe en desarrollo.</summary>
    public const string Development = "Development";
}

/// <summary>Cómo se llama un proveedor y cómo se le nombra en pantalla.</summary>
public sealed record AuthProvider(string Name, string DisplayName);

/// <summary>
/// Proveedores con los que se puede entrar en esta instalación.
/// </summary>
/// <remarks>
/// Se resuelve al arrancar y la pantalla de acceso lo consulta, en lugar de traer los
/// botones escritos a mano. Añadir Apple es añadir una entrada aquí: ni el cliente ni
/// el esquema de la base de datos se enteran.
/// </remarks>
public sealed class AvailableProviders(IReadOnlyList<AuthProvider> providers)
{
    public IReadOnlyList<AuthProvider> All { get; } = providers;

    public bool AllowsDevelopmentSignIn =>
        All.Any(provider => provider.Name == IdentityProviders.Development);
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

        // Se registran siempre: el identificador del usuario de desarrollo lo necesita
        // también la adopción de datos, con proveedor configurado o sin él.
        services.Configure<DevelopmentUserOptions>(
            configuration.GetSection(DevelopmentUserOptions.SectionName));

        // La sesión es siempre la cookie, haya proveedor externo o no. Con un esquema
        // distinto para desarrollo, cerrar sesión no cerraba nada y la caducidad no se
        // podía ni provocar: se probaba un camino que en producción no existe.
        var builder = services
            .AddAuthentication(SessionScheme)
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

                // Cancelar en la pantalla de Google es una decisión, no una avería. Sin
                // esto sale como error del servidor, y quien solo ha cambiado de idea se
                // encuentra un fallo técnico en lugar de la pantalla de la que venía.
                options.Events.OnRemoteFailure = context =>
                {
                    context.Response.Redirect("/signin?error=proveedor");
                    context.HandleResponse();

                    return Task.CompletedTask;
                };
            });
        }

        // El acceso de desarrollo aparece solo si no hay proveedor externo, o si se pide
        // expresamente para tenerlo al lado de Google. La condición de entorno va aparte
        // y primero: una bandera de configuración mal puesta en un despliegue no debe
        // poder abrir esta puerta, y así no llega ni a mirarse.
        var developmentSignIn = isDevelopment
            && (!hasGoogle || configuration.GetValue<bool>(DevelopmentUserOptions.EnabledKey));

        List<AuthProvider> providers = [];

        if (hasGoogle)
        {
            providers.Add(new AuthProvider(IdentityProviders.Google, "Google"));
        }

        if (developmentSignIn)
        {
            providers.Add(new AuthProvider(IdentityProviders.Development, "usuario de desarrollo"));
        }

        services.AddSingleton(new AvailableProviders(providers));

        return builder;
    }

    /// <summary>Marca dónde viaja el nombre del proveedor mientras dura el intercambio.</summary>
    public const string ProviderMarker = "kapea:provider";

    /// <summary>
    /// Con qué proveedor se ha entrado.
    /// </summary>
    /// <remarks>
    /// Sale del marcador que se guardó al salir y no del principal: cuando el proveedor
    /// firma en la cookie, el tipo de autenticación que queda es el del esquema de
    /// sesión. Fiarse de ahí guardaría la identidad a nombre del esquema, y el siguiente
    /// acceso del mismo proveedor no reconocería a nadie.
    /// </remarks>
    public static string ResolveProvider(AuthenticationProperties? properties, ClaimsPrincipal? principal)
    {
        if (properties is not null
            && properties.Items.TryGetValue(ProviderMarker, out var marked)
            && !string.IsNullOrWhiteSpace(marked))
        {
            return marked;
        }

        var declared = principal?.Identity?.AuthenticationType;

        return string.IsNullOrWhiteSpace(declared) || declared == SessionScheme
            ? IdentityProviders.Google
            : declared;
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
