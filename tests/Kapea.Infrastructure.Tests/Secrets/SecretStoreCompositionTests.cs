using Kapea.Application.Abstractions;
using Kapea.Infrastructure;
using Kapea.Infrastructure.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kapea.Infrastructure.Tests.Secrets;

public class SecretStoreCompositionTests
{
    [Fact]
    public void Outside_development_the_managed_store_is_required()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => Compose(isDevelopment: false, vaultUri: null));

        // El mensaje nombra la clave que falta: un arranque fallido sin decir qué
        // configurar obliga a leer el código para desplegar.
        Assert.Contains("KeyVault:Uri", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Outside_development_the_managed_store_is_enough()
    {
        var store = Compose(isDevelopment: false, vaultUri: "https://ejemplo.vault.azure.net/");

        Assert.IsType<KeyVaultSecretStore>(store);
    }

    [Fact]
    public void In_development_the_file_store_is_composed()
    {
        var store = Compose(isDevelopment: true, vaultUri: null);

        Assert.IsType<UserSecretsSecretStore>(store);
    }

    [Fact]
    public void The_managed_store_wins_in_development_too()
    {
        // Es lo que permite probar contra el almacén de verdad sin cambiar de entorno.
        var store = Compose(isDevelopment: true, vaultUri: "https://ejemplo.vault.azure.net/");

        Assert.IsType<KeyVaultSecretStore>(store);
    }

    private static ISecretStore Compose(bool isDevelopment, string? vaultUri)
    {
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:Kapea"] = "Server=(local);Database=Kapea;Trusted_Connection=True",
            ["KeyVault:Uri"] = vaultUri,
            // Fuera del árbol de la solución, para no escribir en el almacén real de
            // quien ejecute las pruebas.
            ["UserSecrets:Id"] = $"kapea-pruebas-{Guid.NewGuid():N}",
        };

        var services = new ServiceCollection()
            .AddLogging()
            .AddKapeaInfrastructure(
                new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
                isDevelopment);

        return services.BuildServiceProvider().GetRequiredService<ISecretStore>();
    }
}
