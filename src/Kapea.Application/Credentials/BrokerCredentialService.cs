using Kapea.Application.Abstractions;
using Kapea.Domain.Accounts;
using Kapea.Domain.Credentials;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Credentials;

/// <summary>La plataforma ha rechazado la credencial en el alta o en la rotación.</summary>
public sealed class CredentialRejectedException(string reason)
    : InvalidOperationException($"La plataforma ha rechazado la credencial: {reason}");

/// <summary>
/// Alta, rotación y revocación de credenciales. Toda credencial se verifica contra su
/// plataforma antes de darse por buena: registrar una que no funciona solo aplaza el
/// fallo hasta la primera sincronización nocturna.
/// </summary>
public sealed class BrokerCredentialService(
    ISecretStore secretStore,
    IEnumerable<ICredentialVerifier> verifiers,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<BrokerCredentialService> logger)
{
    private readonly Dictionary<PlatformCode, ICredentialVerifier> _verifiers =
        verifiers.ToDictionary(verifier => verifier.Platform);

    /// <param name="account">
    /// La cuenta, no su identificador. Para tenerla hay que haberla leído con el filtro
    /// por usuario puesto, así que pasar la de otra persona deja de ser posible.
    /// </param>
    public async Task<BrokerCredential> RegisterAsync(
        PlatformAccount account,
        PlatformCode platform,
        string alias,
        ApiSecret secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(secret);

        if (account.UserId != currentUser.Id)
        {
            throw new UnauthorizedAccessException(
                "La cuenta no es de quien tiene la sesión iniciada.");
        }

        var verification = await VerifyAsync(platform, secret, cancellationToken).ConfigureAwait(false);

        // El nombre del secreto se decide aquí y no lo aporta el llamante: así una
        // petición no puede hacer que la credencial apunte a un secreto ajeno.
        var secretName = SecretNameFor(currentUser.Id, account.Id, platform);

        BrokerCredential.EnsureReadOnly(verification.Scopes);
        await secretStore.SetAsync(secretName, secret, cancellationToken).ConfigureAwait(false);

        var credential = BrokerCredential.Register(
            currentUser.Id, account.Id, platform, alias, secretName, verification.Scopes, timeProvider.GetUtcNow());

        logger.LogInformation("Credencial de {Plataforma} dada de alta para la cuenta {Cuenta}.", platform, account.Id);

        return credential;
    }

    public async Task RotateAsync(
        BrokerCredential credential,
        ApiSecret secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(secret);

        var verification = await VerifyAsync(credential.Platform, secret, cancellationToken).ConfigureAwait(false);

        BrokerCredential.EnsureReadOnly(verification.Scopes);
        await secretStore.SetAsync(credential.SecretName, secret, cancellationToken).ConfigureAwait(false);

        credential.Rotate(credential.SecretName, verification.Scopes, timeProvider.GetUtcNow());

        logger.LogInformation("Credencial {Credencial} rotada.", credential.Id);
    }

    /// <summary>
    /// Revoca la credencial y borra su secreto. Los movimientos importados con ella se
    /// conservan intactos: son datos históricos, no dependen de que la clave siga viva.
    /// </summary>
    public async Task RevokeAsync(BrokerCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        credential.Revoke();
        await secretStore.DeleteAsync(credential.SecretName, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Credencial {Credencial} revocada.", credential.Id);
    }

    /// <summary>Recupera el secreto para usarlo en una sincronización. Vive solo durante la llamada.</summary>
    public async Task<ApiSecret?> ResolveAsync(BrokerCredential credential, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return credential.IsUsable
            ? await secretStore.GetAsync(credential.SecretName, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async Task<CredentialVerification> VerifyAsync(
        PlatformCode platform,
        ApiSecret secret,
        CancellationToken cancellationToken)
    {
        if (!_verifiers.TryGetValue(platform, out var verifier))
        {
            throw new InvalidOperationException($"No hay comprobador de credenciales para {platform}.");
        }

        var verification = await verifier.VerifyAsync(secret, cancellationToken).ConfigureAwait(false);

        if (!verification.IsValid)
        {
            // No se persiste nada: una credencial que la plataforma rechaza no llega a existir.
            logger.LogWarning("La plataforma {Plataforma} ha rechazado una credencial.", platform);

            throw new CredentialRejectedException(verification.Reason ?? "sin motivo indicado");
        }

        return verification;
    }

    /// <summary>
    /// Nombre bajo el que se guarda el secreto.
    /// </summary>
    /// <remarks>
    /// Lleva el usuario además de la cuenta. Con solo la cuenta, dos personas cuyas
    /// credenciales apuntaran al mismo identificador compartirían entrada en el almacén
    /// y una pisaría la clave de la otra sin que nada fallara: la siguiente
    /// sincronización usaría una clave ajena.
    /// </remarks>
    private static string SecretNameFor(UserId userId, Guid accountId, PlatformCode platform) =>
        $"broker-{platform.ToString().ToLowerInvariant()}-{userId.Value:N}-{accountId:N}";
}
