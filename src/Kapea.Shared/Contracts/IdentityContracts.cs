namespace Kapea.Shared.Contracts;

/// <summary>
/// Quién tiene la sesión iniciada.
/// </summary>
/// <remarks>
/// No lleva ningún dato del proveedor más allá de su nombre: ni tokens ni el
/// identificador de sujeto, que no le hacen ninguna falta al navegador.
/// </remarks>
public sealed record CurrentUserResponse(
    Guid Id,
    string DisplayName,
    string? Email,
    IReadOnlyList<LinkedIdentityResponse> Identities);

/// <summary>Proveedor enlazado a la cuenta.</summary>
public sealed record LinkedIdentityResponse(
    Guid Id,
    string Provider,
    DateTimeOffset LinkedAt,
    DateTimeOffset? LastSignedInAt);
