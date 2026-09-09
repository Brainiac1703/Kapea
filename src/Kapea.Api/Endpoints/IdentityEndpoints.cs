using System.Security.Claims;
using Kapea.Api.Authentication;
using Kapea.Application.Abstractions;
using Kapea.Application.Identity;
using Kapea.Domain.Identity;
using Kapea.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;

namespace Kapea.Api.Endpoints;

/// <summary>Acceso, cierre de sesión e identidades enlazadas.</summary>
public static class IdentityEndpoints
{
    private const string LinkMarker = "kapea:link";

    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var identity = app.MapGroup("/auth");

        // Inicio del flujo. No requiere sesión: es justamente lo que la crea.
        identity.MapGet("/signin/{provider}", (string provider, string? returnUrl, HttpContext context) =>
        {
            var properties = new AuthenticationProperties { RedirectUri = $"/auth/callback?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}" };

            return Results.Challenge(properties, [provider]);
        });

        // Enlace de otro proveedor a la sesión actual. Se marca para que la vuelta sepa
        // que no es un acceso, sino un enlace, y no cambie de usuario por el camino.
        identity.MapGet("/link/{provider}", (string provider, HttpContext context) =>
        {
            var properties = new AuthenticationProperties { RedirectUri = "/auth/callback?returnUrl=/identities" };
            properties.Items[LinkMarker] = "1";

            return Results.Challenge(properties, [provider]);
        }).RequireAuthorization();

        identity.MapGet("/callback", async (
            string? returnUrl,
            HttpContext context,
            UserSignInService signIn,
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

            var provider = result.Principal.Identity?.AuthenticationType ?? IdentityProviders.Google;
            var principal = result.Principal.ToExternalPrincipal(provider);

            if (result.Properties?.Items.ContainsKey(LinkMarker) == true)
            {
                await signIn.LinkAsync(currentUser.Id, principal, token);

                return Results.Redirect(returnUrl ?? "/identities");
            }

            var signedIn = await signIn.SignInAsync(principal, token);

            // La cookie se reemite con el identificador interno: a partir de aquí la
            // sesión identifica a un usuario de Kapea, no a una cuenta de Google.
            await context.SignInAsync(KapeaAuthentication.SessionScheme, Session(signedIn.User, provider));

            return Results.Redirect(returnUrl ?? "/");
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
