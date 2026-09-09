using Kapea.Domain.Accounts;
using Kapea.Domain.Credentials;

namespace Kapea.Application.Abstractions;

/// <summary>
/// Almacén de secretos. El resto de la aplicación solo maneja nombres; el secreto
/// aparece únicamente dentro de la llamada que lo necesita y no se persiste en la
/// base de datos ni viaja en ninguna respuesta.
/// </summary>
public interface ISecretStore
{
    Task SetAsync(string name, ApiSecret secret, CancellationToken cancellationToken = default);

    /// <summary>Devuelve null si el secreto no existe: pedir uno borrado no es un error del llamante.</summary>
    Task<ApiSecret?> GetAsync(string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}

/// <summary>
/// Par clave/secreto de una plataforma. Su ToString se sobrescribe para que un
/// registro descuidado o un volcado de diagnóstico no lo filtren.
/// </summary>
public sealed record ApiSecret(string Key, string Secret)
{
    public override string ToString() => "ApiSecret { Key = ***, Secret = *** }";
}

/// <summary>Resultado de comprobar una credencial contra su plataforma.</summary>
/// <param name="IsValid">La plataforma la acepta.</param>
/// <param name="Scopes">Ámbitos que declara. None cuando la plataforma no los expone.</param>
/// <param name="Reason">Motivo del rechazo, para poder enseñárselo al usuario.</param>
public sealed record CredentialVerification(bool IsValid, CredentialScopes Scopes, string? Reason)
{
    public static CredentialVerification Valid(CredentialScopes scopes) => new(true, scopes, null);

    public static CredentialVerification Rejected(string reason) => new(false, CredentialScopes.None, reason);
}

/// <summary>Comprueba una credencial contra la plataforma antes de darla por buena.</summary>
public interface ICredentialVerifier
{
    Platform Platform { get; }

    Task<CredentialVerification> VerifyAsync(ApiSecret secret, CancellationToken cancellationToken = default);
}
