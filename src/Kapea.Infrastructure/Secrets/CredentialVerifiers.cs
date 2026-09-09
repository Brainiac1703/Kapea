using System.Net;
using Kapea.Application.Abstractions;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Credentials;
using Kapea.Infrastructure.Import.Bit2Me;
using Kapea.Infrastructure.Import.Kraken;

namespace Kapea.Infrastructure.Secrets;

/// <summary>
/// Comprueba una credencial de Kraken pidiendo el histórico más corto posible.
/// </summary>
/// <remarks>
/// Kraken no expone los ámbitos de una clave, así que una credencial que responde se
/// da por de solo lectura. La comprobación de permisos excesivos solo puede aplicarse
/// donde la plataforma los declara; aquí se apoya en que el usuario cree la clave con
/// permisos mínimos, tal y como indica el alta.
/// </remarks>
public sealed class KrakenCredentialVerifier(KrakenApiClient client) : ICredentialVerifier
{
    public Platform Platform => Platform.Kraken;

    public async Task<CredentialVerification> VerifyAsync(
        ApiSecret secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var now = DateTimeOffset.UtcNow;

        try
        {
            await client
                .GetTradesAsync(new ApiCredential(secret.Key, secret.Secret), now.AddMinutes(-1), now, cancellationToken)
                .ConfigureAwait(false);

            return CredentialVerification.Valid(CredentialScopes.Read);
        }
        catch (KrakenApiException exception)
        {
            return CredentialVerification.Rejected(exception.Message);
        }
        catch (FormatException)
        {
            // El secreto de Kraken viaja en base64; uno mal copiado ni siquiera llega a
            // firmarse, y decirlo así ahorra buscar el problema en el lado equivocado.
            return CredentialVerification.Rejected("El secreto no está en base64: revisa que se haya copiado entero.");
        }
    }
}

/// <summary>Comprueba una credencial de Bit2Me pidiendo el saldo de contado.</summary>
public sealed class Bit2MeCredentialVerifier(Bit2MeApiClient client) : ICredentialVerifier
{
    public Platform Platform => Platform.Bit2Me;

    public async Task<CredentialVerification> VerifyAsync(
        ApiSecret secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var now = DateTimeOffset.UtcNow;

        try
        {
            await client
                .GetTradesAsync(new ApiCredential(secret.Key, secret.Secret), now.AddMinutes(-1), now, cancellationToken)
                .ConfigureAwait(false);

            return CredentialVerification.Valid(CredentialScopes.Read);
        }
        catch (Bit2MeAccessDeniedException exception)
        {
            return exception.Status == HttpStatusCode.Forbidden
                ? CredentialVerification.Rejected("La credencial no tiene permiso de lectura sobre el histórico.")
                : CredentialVerification.Rejected(exception.Message);
        }
    }
}
