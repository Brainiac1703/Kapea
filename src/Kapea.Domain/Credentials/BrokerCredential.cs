using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Credentials;

/// <summary>Estado de una credencial frente a la plataforma.</summary>
public enum CredentialStatus
{
    /// <summary>Verificada y utilizable en las sincronizaciones.</summary>
    Active = 1,

    /// <summary>El usuario la ha retirado. No vuelve a usarse, pero lo importado con ella se conserva.</summary>
    Revoked = 2,

    /// <summary>La plataforma la ha rechazado durante una sincronización.</summary>
    Invalid = 3,
}

/// <summary>Ámbitos que la plataforma declara para una credencial.</summary>
[Flags]
public enum CredentialScopes
{
    None = 0,
    Read = 1,
    Trade = 2,
    Withdraw = 4,
}

/// <summary>
/// Credencial de acceso a una plataforma. Guarda metadatos y el nombre bajo el que
/// vive el secreto en el almacén, nunca el secreto.
/// </summary>
/// <remarks>
/// La separación no es cosmética: el cliente Blazor WebAssembly corre en el navegador
/// del usuario y no puede custodiar nada, así que el secreto no debe existir en ninguna
/// forma que pueda llegar hasta él por accidente.
/// </remarks>
public sealed class BrokerCredential
{
    private BrokerCredential()
    {
        Alias = null!;
        SecretName = null!;
    }

    private BrokerCredential(
        Guid id,
        UserId userId,
        Guid accountId,
        Platform platform,
        string alias,
        string secretName,
        CredentialScopes scopes,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        AccountId = accountId;
        Platform = platform;
        Alias = alias;
        SecretName = secretName;
        Scopes = scopes;
        CreatedAt = createdAt;
        Status = CredentialStatus.Active;
    }

    public Guid Id { get; private set; }

    public UserId UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Platform Platform { get; private set; }

    public string Alias { get; private set; }

    /// <summary>Nombre bajo el que el almacén guarda el secreto. No es el secreto.</summary>
    public string SecretName { get; private set; }

    public CredentialScopes Scopes { get; private set; }

    public CredentialStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RotatedAt { get; private set; }

    public DateTimeOffset? LastSynchronizedAt { get; private set; }

    /// <summary>Motivo por el que la credencial dejó de servir. Visible para el usuario.</summary>
    public string? InvalidReason { get; private set; }

    /// <summary>Solo una credencial activa entra en las sincronizaciones programadas.</summary>
    public bool IsUsable => Status == CredentialStatus.Active;

    public static BrokerCredential Register(
        UserId userId,
        Guid accountId,
        Platform platform,
        string alias,
        string secretName,
        CredentialScopes scopes,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DomainException("Una credencial necesita alias para distinguirla de otras de la misma cuenta.");
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new DomainException("Una credencial necesita el nombre bajo el que se guarda su secreto.");
        }

        EnsureReadOnly(scopes);

        return new BrokerCredential(
            Guid.NewGuid(), userId, accountId, platform, alias.Trim(), secretName.Trim(), scopes, createdAt);
    }

    /// <summary>
    /// Kapea solo lee. Una credencial que puede operar o retirar fondos es un riesgo
    /// que la aplicación no necesita correr, así que se rechaza en el alta.
    /// </summary>
    public static void EnsureReadOnly(CredentialScopes scopes)
    {
        if (scopes.HasFlag(CredentialScopes.Trade) || scopes.HasFlag(CredentialScopes.Withdraw))
        {
            throw new DomainException(
                "Kapea solo admite credenciales de solo lectura: esta declara permisos de operación o de retirada.");
        }
    }

    /// <summary>Sustituye el secreto conservando la identidad y el histórico de la credencial.</summary>
    public void Rotate(string secretName, CredentialScopes scopes, DateTimeOffset rotatedAt)
    {
        if (Status == CredentialStatus.Revoked)
        {
            throw new DomainException("Una credencial revocada no se rota: se da de alta una nueva.");
        }

        if (string.IsNullOrWhiteSpace(secretName))
        {
            throw new DomainException("Una credencial necesita el nombre bajo el que se guarda su secreto.");
        }

        EnsureReadOnly(scopes);

        SecretName = secretName.Trim();
        Scopes = scopes;
        Status = CredentialStatus.Active;
        InvalidReason = null;
        RotatedAt = rotatedAt;
    }

    public void Revoke()
    {
        Status = CredentialStatus.Revoked;
        InvalidReason = null;
    }

    /// <summary>
    /// La plataforma ha rechazado la credencial. Se aparta de las sincronizaciones y se
    /// avisa, sin tocar nada de lo ya importado con ella.
    /// </summary>
    public void MarkInvalid(string reason)
    {
        if (Status == CredentialStatus.Revoked)
        {
            return;
        }

        Status = CredentialStatus.Invalid;
        InvalidReason = string.IsNullOrWhiteSpace(reason) ? "La plataforma ha rechazado la credencial." : reason.Trim();
    }

    public void MarkSynchronized(DateTimeOffset synchronizedAt) => LastSynchronizedAt = synchronizedAt;
}
