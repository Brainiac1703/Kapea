using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Identity;

/// <summary>
/// Persona que usa Kapea. Es a su identificador al que cuelgan las cuentas, los
/// movimientos, los lotes y los resultados.
/// </summary>
/// <remarks>
/// El identificador es propio y no el que devuelve el proveedor. Si los datos
/// colgaran del identificador de Google, enlazar otro proveedor obligaría a reasignar
/// todo el histórico, y dejar de usar Google lo dejaría huérfano.
/// </remarks>
public sealed class User
{
    private readonly List<ExternalIdentity> _identities = [];

    private User()
    {
        DisplayName = null!;
    }

    private User(UserId id, string displayName, string? email, DateTimeOffset createdAt)
    {
        Id = id;
        DisplayName = displayName;
        Email = email;
        CreatedAt = createdAt;
    }

    public UserId Id { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>Correo del proveedor. Se guarda para mostrarlo, nunca para identificar.</summary>
    public string? Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastSignedInAt { get; private set; }

    public IReadOnlyList<ExternalIdentity> Identities => _identities;

    /// <summary>Alta a partir del primer acceso con un proveedor.</summary>
    public static User Register(
        string provider,
        string subject,
        string? displayName,
        string? email,
        DateTimeOffset createdAt)
    {
        var user = new User(
            new UserId(Guid.NewGuid()),
            NormalizeName(displayName, email),
            NormalizeEmail(email),
            createdAt);

        user._identities.Add(ExternalIdentity.Link(provider, subject, createdAt));

        return user;
    }

    public ExternalIdentity? FindIdentity(string provider, string subject) =>
        _identities.SingleOrDefault(identity => identity.Matches(provider, subject));

    /// <summary>Enlaza otro proveedor. Es lo que evita que perder una cuenta signifique perder el histórico.</summary>
    public ExternalIdentity LinkIdentity(string provider, string subject, DateTimeOffset linkedAt)
    {
        if (FindIdentity(provider, subject) is not null)
        {
            throw new DomainException("Esa identidad ya está enlazada a este usuario.");
        }

        var identity = ExternalIdentity.Link(provider, subject, linkedAt);
        _identities.Add(identity);

        return identity;
    }

    /// <summary>
    /// Desenlaza un proveedor. Se impide quitar el último: dejaría al usuario sin
    /// ninguna forma de acceder a sus propios datos, y sin contraseña propia no habría
    /// manera de recuperarlos.
    /// </summary>
    public void UnlinkIdentity(Guid identityId)
    {
        var identity = _identities.SingleOrDefault(candidate => candidate.Id == identityId)
            ?? throw new DomainException("Esa identidad no pertenece a este usuario.");

        if (_identities.Count == 1)
        {
            throw new DomainException(
                "No se puede quitar la única forma de acceder a la cuenta. Enlaza otro proveedor antes.");
        }

        _identities.Remove(identity);
    }

    /// <summary>
    /// Registra el acceso y refresca lo que el proveedor haya cambiado. El correo se
    /// actualiza porque solo sirve para mostrarlo; la identidad sigue siendo el sujeto.
    /// </summary>
    public void RecordSignIn(ExternalIdentity identity, string? displayName, string? email, DateTimeOffset signedInAt)
    {
        ArgumentNullException.ThrowIfNull(identity);

        identity.RecordSignIn(signedInAt);
        LastSignedInAt = signedInAt;

        if (NormalizeEmail(email) is { } refreshed)
        {
            Email = refreshed;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }
    }

    private static string NormalizeName(string? displayName, string? email) =>
        string.IsNullOrWhiteSpace(displayName)
            ? NormalizeEmail(email) ?? "Usuario"
            : displayName.Trim();

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
