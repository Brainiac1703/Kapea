using Kapea.Api.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kapea.Api.Tests.Hosting;

public class SessionKeysTests
{
    [Fact]
    public void Without_configuration_the_keys_stay_in_memory() =>
        Assert.Equal(SessionKeyStorage.Memory, Choose([]));

    [Fact]
    public void A_directory_keeps_the_keys_on_disk() =>
        Assert.Equal(
            SessionKeyStorage.FileSystem,
            Choose(new Dictionary<string, string?> { [SessionKeys.KeysPathKey] = "/tmp/kapea-keys" }));

    [Fact]
    public void A_blob_is_what_survives_a_new_revision() =>
        Assert.Equal(
            SessionKeyStorage.Blob,
            Choose(new Dictionary<string, string?>
            {
                [SessionKeys.BlobUriKey] = "https://ejemplo.blob.core.windows.net/data-protection/keys.xml",
                [SessionKeys.KeyUriKey] = "https://ejemplo.vault.azure.net/keys/data-protection",
            }));

    [Fact]
    public void The_blob_wins_over_the_directory()
    {
        // En Azure el contenedor lleva el directorio del compose heredado en la imagen.
        // Si ganara el directorio, las claves volverían a morir con cada revisión.
        var storage = Choose(new Dictionary<string, string?>
        {
            [SessionKeys.KeysPathKey] = "/home/app/.aspnet/DataProtection-Keys",
            [SessionKeys.BlobUriKey] = "https://ejemplo.blob.core.windows.net/data-protection/keys.xml",
        });

        Assert.Equal(SessionKeyStorage.Blob, storage);
    }

    private static SessionKeyStorage Choose(Dictionary<string, string?> settings) =>
        new ServiceCollection().AddKapeaSessionKeys(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
