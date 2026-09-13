using System.Security.Claims;
using Kapea.Api.Authentication;
using Kapea.Application.Abstractions;
using Kapea.Application.Identity;
using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;
using Kapea.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kapea.Api.Endpoints;

/// <summary>Acceso, cierre de sesión e identidades enlazadas.</summary>
public static class IdentityEndpoints
{
    private const string LinkMarker = "kapea:link";

    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var identity = app.MapGroup("/auth");

        // Con qué se puede entrar aquí. Sin sesión, claro: es lo primero que se pregunta.
        identity.MapGet("/providers", (AvailableProviders providers) =>
            Results.Ok(providers.All
                .Select(provider => new AuthProviderResponse(provider.Name, provider.DisplayName))
                .ToArray()));

        // Inicio del flujo. No requiere sesión: es justamente lo que la crea.
        identity.MapGet("/signin/{provider}", async (
            string provider,
            string? returnUrl,
            HttpContext context,
            AvailableProviders providers,
            UserSignInService signIn,
            DevelopmentDataAdoption adoption,
            IOptions<DevelopmentUserOptions> developmentUser,
            CancellationToken token) =>
        {
            // El acceso de desarrollo no sale a ningún sitio, pero de ahí en adelante
            // recorre lo mismo que la vuelta de un proveedor: se registra o se reconoce,
            // queda anotado el acceso y la sesión sale en la misma cookie. Un atajo que
            // se saltara todo eso probaría un camino que en producción no existe.
            if (string.Equals(provider, IdentityProviders.Development, StringComparison.OrdinalIgnoreCase))
            {
                if (!providers.AllowsDevelopmentSignIn)
                {
                    return Results.NotFound();
                }

                await CompleteSignInAsync(
                    context,
                    signIn,
                    adoption,
                    developmentUser.Value,
                    new ExternalPrincipal(
                        IdentityProviders.Development,
                        DevelopmentUserOptions.Subject,
                        developmentUser.Value.DisplayName,
                        Email: null),
                    token);

                return Results.Redirect(returnUrl ?? "/");
            }

            var properties = new AuthenticationProperties { RedirectUri = $"/auth/callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}" };
            properties.Items[KapeaAuthentication.ProviderMarker] = provider;

            return Results.Challenge(properties, [provider]);
        });

        // Enlace de otro proveedor a la sesión actual. Se marca para que la vuelta sepa
        // que no es un acceso, sino un enlace, y no cambie de usuario por el camino.
        identity.MapGet("/link/{provider}", (string provider, HttpContext context) =>
        {
            var properties = new AuthenticationProperties { RedirectUri = "/auth/callback?returnUrl=/identities" };
            properties.Items[LinkMarker] = "1";
            properties.Items[KapeaAuthentication.ProviderMarker] = provider;

            return Results.Challenge(properties, [provider]);
        }).RequireAuthorization();

        identity.MapGet("/callback", async (
            string? returnUrl,
            HttpContext context,
            UserSignInService signIn,
            DevelopmentDataAdoption adoption,
            IOptions<DevelopmentUserOptions> developmentUser,
            ICurrentUser currentUser,
            CancellationToken token) =>
        {
            var result = await context.AuthenticateAsync(KapeaAuthentication.SessionScheme);

            if (!result.Succeeded || result.Principal is null)
            {
                return Results.Problem(
                    "No se ha podido completar el acceso con el proveedor.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var provider = KapeaAuthentication.ResolveProvider(result.Properties, result.Principal);
            var principal = result.Principal.ToExternalPrincipal(provider);

            if (result.Properties?.Items.ContainsKey(LinkMarker) == true)
            {
                await signIn.LinkAsync(currentUser.Id, principal, token);

                return Results.Redirect(returnUrl ?? "/identities");
            }

            await CompleteSignInAsync(context, signIn, adoption, developmentUser.Value, principal, token);

            return Results.Redirect(returnUrl ?? "/");
        });

        // Cierre desde el navegador: cierra la sesión y devuelve a la pantalla de acceso.
        identity.MapGet("/signout-redirect", async (HttpContext context) =>
        {
            await context.SignOutAsync(KapeaAuthentication.SessionScheme);

            return Results.Redirect("/signin");
        });

        identity.MapPost("/signout", async (HttpContext context) =>
        {
            await context.SignOutAsync(KapeaAuthentication.SessionScheme);

            return Results.NoContent();
        }).RequireAuthorization();

        var me = app.MapGroup("/api/me").RequireAuthorization();

        me.MapGet("/", async (ICurrentUser currentUser, IUserRepository users, CancellationToken token) =>
            await users.FindAsync(currentUser.Id, token) is { } user
                ? Results.Ok(ToResponse(user))
                : Results.Unauthorized());

        me.MapDelete("/identities/{identityId:guid}", async (
            Guid identityId,
            ICurrentUser currentUser,
            UserSignInService signIn,
            IUserRepository users,
            CancellationToken token) =>
        {
            await signIn.UnlinkAsync(currentUser.Id, identityId, token);

            return Results.Ok(ToResponse((await users.FindAsync(currentUser.Id, token))!));
        });
    }

    /// <summary>Registra o reconoce a quien entra y le deja la sesión en la cookie.</summary>
    private static async Task CompleteSignInAsync(
        HttpContext context,
        UserSignInService signIn,
        DevelopmentDataAdoption adoption,
        DevelopmentUserOptions developmentUser,
        ExternalPrincipal principal,
        CancellationToken token)
    {
        var signedIn = await signIn.SignInAsync(principal, token);

        if (signedIn.Outcome == SignInOutcome.Registered)
        {
            // Lo importado antes de que hubiera usuarios está a nombre de un propietario
            // que ya no existe. El primero que entra lo adopta, para no perder el
            // histórico de pruebas.
            await adoption.AdoptAsync(signedIn.User, new UserId(developmentUser.LegacyOwnerId), token);
        }

        // La cookie se emite con el identificador interno: a partir de aquí la sesión
        // identifica a un usuario de Kapea, no a una cuenta del proveedor.
        await context.SignInAsync(
            KapeaAuthentication.SessionScheme, Session(signedIn.User, principal.Provider));
    }

    private static ClaimsPrincipal Session(User user, string provider)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(KapeaAuthentication.UserIdClaim, user.Id.Value.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.AuthenticationMethod, provider),
            ],
            KapeaAuthentication.SessionScheme);

        if (user.Email is { Length: > 0 } email)
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, email));
        }

        return new ClaimsPrincipal(identity);
    }

    private static CurrentUserResponse ToResponse(User user) =>
        new(
            user.Id.Value,
            user.DisplayName,
            user.Email,
            [
                .. user.Identities.Select(identity => new LinkedIdentityResponse(
                    identity.Id, identity.Provider, identity.LinkedAt, identity.LastSignedInAt)),
            ]);
}
