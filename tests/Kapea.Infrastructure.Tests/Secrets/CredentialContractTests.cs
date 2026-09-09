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
            new UserId(Guid.NewGuid()), Guid.NewGuid(), Platform.Kraken, "Kraken principal",
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
            new UserId(Guid.NewGuid()), Guid.NewGuid(), Platform.Bit2Me, "Bit2Me",
            "broker-bit2me-abc", CredentialScopes.Read, DateTimeOffset.UtcNow);

        Assert.Equal("broker-bit2me-abc", credential.SecretName);
        Assert.DoesNotContain(
            typeof(BrokerCredential).GetProperties(),
            property => property.Name.Equals("Secret", StringComparison.OrdinalIgnoreCase));
    }
}
