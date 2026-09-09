using Kapea.Domain.Common;
using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Identity;

/// <summary>Datos que entrega el proveedor tras un acceso correcto.</summary>
/// <param name="Provider">Proveedor que emitió la identidad.</param>
/// <param name="Subject">Identificador de sujeto. Es lo único que identifica.</param>
/// <param name="DisplayName">Nombre para mostrar, si lo entrega.</param>
/// <param name="Email">Correo, si lo entrega. Solo para mostrarlo.</param>
public sealed record ExternalPrincipal(string Provider, string Subject, string? DisplayName, string? Email);

/// <summary>Qué ocurrió al resolver el acceso.</summary>
public enum SignInOutcome
{
    /// <summary>Se ha creado el usuario porque era su primer acceso.</summary>
    Registered = 1,

    /// <summary>Se ha reconocido a un usuario existente.</summary>
    SignedIn = 2,
}

public sealed record SignInResult(User User, SignInOutcome Outcome);

/// <summary>Intento de enlazar una identidad que ya pertenece a otra persona.</summary>
public sealed class IdentityAlreadyLinkedException(string provider)
    : DomainException($"Esa cuenta de {provider} ya está enlazada a otro usuario de Kapea.");

/// <summary>
/// Resuelve quién entra: crea el usuario en el primer acceso y lo reconoce en los
/// siguientes.
/// </summary>
/// <remarks>
/// La búsqueda es siempre por proveedor y sujeto. Buscar por correo dejaría entrar a
/// quien controle esa dirección en otro proveedor, que es la forma más fácil de
/// suplantar a alguien en una aplicación con acceso social.
/// </remarks>
public sealed class UserSignInService(
    IUserRepository repository,
    TimeProvider timeProvider,
    ILogger<UserSignInService> logger)
{
    public async Task<SignInResult> SignInAsync(
        ExternalPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var now = timeProvider.GetUtcNow();
        var existing = await repository
            .FindByIdentityAsync(principal.Provider, principal.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            var identity = existing.FindIdentity(principal.Provider, principal.Subject)!;
            existing.RecordSignIn(identity, principal.DisplayName, principal.Email, now);

            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new SignInResult(existing, SignInOutcome.SignedIn);
        }

        var user = User.Register(
            principal.Provider, principal.Subject, principal.DisplayName, principal.Email, now);

        await repository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Alta de usuario {Usuario} con el proveedor {Proveedor}.", user.Id, principal.Provider);

        return new SignInResult(user, SignInOutcome.Registered);
    }

    /// <summary>Enlaza otro proveedor al usuario indicado, si nadie más lo tiene.</summary>
    public async Task<ExternalIdentity> LinkAsync(
        UserId userId,
        ExternalPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var owner = await repository
            .FindByIdentityAsync(principal.Provider, principal.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (owner is not null && owner.Id != userId)
        {
            // No se mueve ningún dato entre usuarios: se rechaza y se dice.
            throw new IdentityAlreadyLinkedException(principal.Provider);
        }

        var user = await repository.FindAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("El usuario no existe.");

        var identity = user.LinkIdentity(principal.Provider, principal.Subject, timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Usuario {Usuario} enlaza el proveedor {Proveedor}.", userId, principal.Provider);

        return identity;
    }

    public async Task UnlinkAsync(UserId userId, Guid identityId, CancellationToken cancellationToken = default)
    {
        var user = await repository.FindAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("El usuario no existe.");

        user.UnlinkIdentity(identityId);

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
