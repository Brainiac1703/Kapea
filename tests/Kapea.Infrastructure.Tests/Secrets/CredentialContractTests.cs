using System.Text.Json;
using Kapea.Domain.Accounts;
using Kapea.Domain.Credentials;
using Kapea.Domain.ValueObjects;
using Kapea.Shared.Contracts;

namespace Kapea.Infrastructure.Tests.Secrets;

public class CredentialContractTests
{
    [Fact]
    public void The_response_contract_has_no_field_able_to_carry_a_secret()
    {
        var forbidden = new[] { "secret", "apikey", "key", "password", "token" };

        var offenders = typeof(BrokerCredentialResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Serialising_a_credential_response_never_emits_a_secret()
    {
        var credential = BrokerCredential.Register(
            new UserId(Guid.NewGuid()), Guid.NewGuid(), PlatformCode.Kraken, "Kraken principal",
            "broker-kraken-abc", CredentialScopes.Read, DateTimeOffset.UtcNow);

        var response = new BrokerCredentialResponse(
            credential.Id, credential.AccountId, credential.Platform.ToString(), credential.Alias,
            credential.Scopes.ToString(), credential.Status.ToString(), credential.CreatedAt,
            credential.RotatedAt, credential.LastSynchronizedAt, credential.InvalidReason);

        var json = JsonSerializer.Serialize(response);

        Assert.DoesNotContain("secreto", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kraken principal", json, StringComparison.Ordinal);
        Assert.Contains("Active", json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_credential_entity_only_keeps_the_name_of_its_secret()
    {
        var credential = BrokerCredential.Register(
            new UserId(Guid.NewGuid()), Guid.NewGuid(), PlatformCode.Bit2Me, "Bit2Me",
            "broker-bit2me-abc", CredentialScopes.Read, DateTimeOffset.UtcNow);

        Assert.Equal("broker-bit2me-abc", credential.SecretName);
        Assert.DoesNotContain(
            typeof(BrokerCredential).GetProperties(),
            property => property.Name.Equals("Secret", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Comprueba que el almacén local avisa pronto si no puede escribir.
/// </summary>
/// <remarks>
/// Existe por un fallo real: en docker compose el volumen se creaba con otro
/// propietario, y el alta de una credencial fallaba con una ruta denegada que no decía
/// qué había que arreglar.
/// </remarks>
public class UserSecretsWritabilityTests
{
    [Fact]
    public void A_writable_store_passes()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;

        try
        {
            new Kapea.Infrastructure.Secrets.UserSecretsSecretStore(
                Path.Combine(directory, "secrets.json")).EnsureWritable();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_store_that_cannot_be_written_says_what_to_do()
    {
        // Un fichero donde debería haber un directorio: no se puede crear dentro, que es
        // lo mismo que le pasa a un volumen montado con otro propietario.
        var blocker = Path.Combine(Path.GetTempPath(), $"kapea-{Guid.NewGuid():N}");
        File.WriteAllText(blocker, string.Empty);

        try
        {
            var store = new Kapea.Infrastructure.Secrets.UserSecretsSecretStore(
                Path.Combine(blocker, "secrets.json"));

            var exception = Assert.Throws<InvalidOperationException>(store.EnsureWritable);

            Assert.Contains("almacén de credenciales", exception.Message, StringComparison.Ordinal);
            Assert.Contains("docker volume rm", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(blocker);
        }
    }
}
