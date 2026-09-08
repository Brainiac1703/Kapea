using Kapea.Application.Import;
using Kapea.Domain.Accounts;

namespace Kapea.Application.Tests.Import;

public class ImportAdapterRegistryTests
{
    [Fact]
    public void A_platform_without_adapter_is_rejected_listing_the_supported_ones()
    {
        var registry = new ImportAdapterRegistry([new FakeFileAdapter(Platform.Xtb)], [new FakeApiAdapter(Platform.Kraken)]);

        var exception = Assert.Throws<UnsupportedPlatformException>(() => registry.GetApiAdapter(Platform.Bit2Me));

        Assert.Contains("Xtb", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kraken", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Platform.Bit2Me, exception.Platform);
    }

    [Fact]
    public void Asking_for_a_file_adapter_of_an_api_only_platform_is_rejected()
    {
        var registry = new ImportAdapterRegistry([], [new FakeApiAdapter(Platform.Kraken)]);

        var exception = Assert.Throws<UnsupportedPlatformException>(() => registry.GetFileAdapter(Platform.Kraken));

        Assert.Equal(ImportSourceKind.UploadedFile, exception.SourceKind);
    }

    [Fact]
    public void A_newly_registered_adapter_is_resolved_without_touching_the_registry()
    {
        // Añadir una plataforma es registrar su adaptador: ni el registro ni nada
        // aguas abajo necesita conocerla de antemano.
        var registry = new ImportAdapterRegistry([], [new FakeApiAdapter(Platform.Bit2Me)]);

        Assert.True(registry.Supports(Platform.Bit2Me));
        Assert.Equal(Platform.Bit2Me, registry.GetApiAdapter(Platform.Bit2Me).Platform);
        Assert.Equal([Platform.Bit2Me], registry.SupportedPlatforms);
    }

    [Fact]
    public void An_empty_registry_supports_nothing() =>
        Assert.Empty(new ImportAdapterRegistry([], []).SupportedPlatforms);

    private sealed class FakeFileAdapter(Platform platform) : IFileImportAdapter
    {
        public Platform Platform => platform;

        public ImportSourceKind SourceKind => ImportSourceKind.UploadedFile;

        public Task<ImportReadResult> ReadAsync(Stream content, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportReadResult.Empty);
    }

    private sealed class FakeApiAdapter(Platform platform) : IApiImportAdapter
    {
        public Platform Platform => platform;

        public ImportSourceKind SourceKind => ImportSourceKind.RemoteApi;

        public Task<ImportReadResult> ReadAsync(
            ApiCredential credential, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult(ImportReadResult.Empty);
    }
}
