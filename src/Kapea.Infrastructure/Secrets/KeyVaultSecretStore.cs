using System.Text.Json;
using Azure;
using Azure.Security.KeyVault.Secrets;
using Kapea.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Secrets;

/// <summary>
/// Guarda los secretos en Azure Key Vault. Es el almacén de producción: la base de
/// datos solo conserva el nombre bajo el que vive cada secreto.
/// </summary>
/// <remarks>
/// Clave y secreto se serializan juntos en un único secreto del almacén para que la
/// rotación sea atómica: dos secretos separados podrían quedar descompasados si la
/// segunda escritura falla.
/// </remarks>
public sealed class KeyVaultSecretStore(SecretClient client, ILogger<KeyVaultSecretStore> logger) : ISecretStore
{
    public async Task SetAsync(string name, ApiSecret secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(secret);

        await client.SetSecretAsync(name, JsonSerializer.Serialize(secret), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Secreto '{Nombre}' guardado en Key Vault.", name);
    }

    public async Task<ApiSecret?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            var response = await client.GetSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);

            return JsonSerializer.Deserialize<ApiSecret>(response.Value.Value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            var operation = await client.StartDeleteSecretAsync(name, cancellationToken).ConfigureAwait(false);
            await operation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Secreto '{Nombre}' eliminado de Key Vault.", name);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // Borrar lo que ya no está es el resultado que se pedía.
        }
    }
}
