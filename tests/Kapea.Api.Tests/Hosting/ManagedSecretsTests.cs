using Azure.Security.KeyVault.Secrets;
using Kapea.Api.Hosting;

namespace Kapea.Api.Tests.Hosting;

public class ManagedSecretsTests
{
    private readonly ManagedSecrets.AuthenticationSecrets _manager = new();

    [Fact]
    public void The_provider_secret_is_loaded() =>
        Assert.True(_manager.Load(new SecretProperties("Authentication--Google--ClientSecret")));

    [Fact]
    public void A_broker_credential_is_not_loaded()
    {
        // Viven en el mismo almacén y se leen por el puerto que las pide una a una. Aquí
        // sólo añadirían un montón de secretos cargados en memoria sin que nadie los use.
        Assert.False(_manager.Load(new SecretProperties("broker-kraken-4f2a")));
    }

    [Fact]
    public void Two_dashes_become_the_configuration_separator()
    {
        // Un nombre de secreto no admite dos puntos, así que la jerarquía se escribe con
        // guiones y se traduce al leerla.
        var key = _manager.GetKey(new KeyVaultSecret("Authentication--Google--ClientSecret", "valor"));

        Assert.Equal("Authentication:Google:ClientSecret", key);
    }
}
