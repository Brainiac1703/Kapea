using System.Text.Json;
using Kapea.Application.Abstractions;

namespace Kapea.Infrastructure.Secrets;

/// <summary>
/// Almacén de desarrollo local: el fichero de User Secrets del proyecto, el mismo que
/// gestiona `dotnet user-secrets`.
/// </summary>
/// <remarks>
/// Guarda en claro, como todo User Secrets. Es aceptable en una máquina de desarrollo
/// —evita montar un Key Vault para probar— y por eso está fuera del árbol del proyecto,
/// donde no puede acabar en un commit. En producción manda
/// <see cref="KeyVaultSecretStore"/>.
/// </remarks>
public sealed class UserSecretsSecretStore : ISecretStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public UserSecretsSecretStore(string secretsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsFilePath);

        _path = secretsFilePath;
    }

    /// <summary>
    /// Comprueba que se puede escribir donde van las credenciales.
    /// </summary>
    /// <remarks>
    /// Se hace al arrancar y no al guardar la primera credencial. Dentro de un
    /// contenedor, un volumen montado con otro propietario deja el almacén de solo
    /// lectura, y sin esta comprobación el fallo aparece mucho después: al dar de alta
    /// una credencial, en forma de ruta denegada que no dice qué hacer.
    /// </remarks>
    public void EnsureWritable()
    {
        var directory = Path.GetDirectoryName(_path)!;

        try
        {
            Directory.CreateDirectory(directory);

            var probe = Path.Combine(directory, $".escritura-{Guid.NewGuid():N}");

            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"No se puede escribir en el almacén de credenciales ({directory}). " +
                "En docker compose suele significar que el volumen se creó con otro propietario: " +
                "bórralo con «docker volume rm kapea_broker-secrets» y vuelve a levantar el entorno.",
                exception);
        }
    }

    /// <summary>Ruta que usa `dotnet user-secrets` para un identificador dado.</summary>
    public static string DefaultPathFor(string userSecretsId) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".microsoft", "usersecrets", userSecretsId, "secrets.json");

    public async Task SetAsync(string name, ApiSecret secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(secret);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var secrets = await ReadAsync(cancellationToken).ConfigureAwait(false);
            secrets[Key(name)] = secret;

            await WriteAsync(secrets, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ApiSecret?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var secrets = await ReadAsync(cancellationToken).ConfigureAwait(false);

        return secrets.GetValueOrDefault(Key(name));
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var secrets = await ReadAsync(cancellationToken).ConfigureAwait(false);

            if (secrets.Remove(Key(name)))
            {
                await WriteAsync(secrets, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Prefijo de sección, para no mezclarse con el resto de secretos del proyecto.</summary>
    private static string Key(string name) => "Kapea:BrokerCredentials:" + name;

    private async Task<Dictionary<string, ApiSecret>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, ApiSecret>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(_path);

        return await JsonSerializer
            .DeserializeAsync<Dictionary<string, ApiSecret>>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false) ?? new Dictionary<string, ApiSecret>(StringComparer.Ordinal);
    }

    private async Task WriteAsync(Dictionary<string, ApiSecret> secrets, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        await using var stream = File.Create(_path);

        await JsonSerializer
            .SerializeAsync(stream, secrets, new JsonSerializerOptions { WriteIndented = true }, cancellationToken)
            .ConfigureAwait(false);
    }
}
