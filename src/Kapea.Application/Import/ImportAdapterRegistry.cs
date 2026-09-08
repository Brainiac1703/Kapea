using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>
/// Selecciona el adaptador por la plataforma de la cuenta destino. Es el punto de
/// extensión del módulo de importación: dar de alta una plataforma nueva es registrar
/// su adaptador, sin tocar el motor, el dominio ni el cálculo.
/// </summary>
public interface IImportAdapterRegistry
{
    IReadOnlyCollection<Platform> SupportedPlatforms { get; }

    IFileImportAdapter GetFileAdapter(Platform platform);

    IApiImportAdapter GetApiAdapter(Platform platform);

    bool Supports(Platform platform);
}

public sealed class ImportAdapterRegistry : IImportAdapterRegistry
{
    private readonly Dictionary<Platform, IFileImportAdapter> _fileAdapters;
    private readonly Dictionary<Platform, IApiImportAdapter> _apiAdapters;

    public ImportAdapterRegistry(IEnumerable<IFileImportAdapter> fileAdapters, IEnumerable<IApiImportAdapter> apiAdapters)
    {
        ArgumentNullException.ThrowIfNull(fileAdapters);
        ArgumentNullException.ThrowIfNull(apiAdapters);

        _fileAdapters = fileAdapters.ToDictionary(adapter => adapter.Platform);
        _apiAdapters = apiAdapters.ToDictionary(adapter => adapter.Platform);
    }

    public IReadOnlyCollection<Platform> SupportedPlatforms =>
        [.. _fileAdapters.Keys.Concat(_apiAdapters.Keys).Distinct().Order()];

    public bool Supports(Platform platform) =>
        _fileAdapters.ContainsKey(platform) || _apiAdapters.ContainsKey(platform);

    public IFileImportAdapter GetFileAdapter(Platform platform) =>
        _fileAdapters.TryGetValue(platform, out var adapter)
            ? adapter
            : throw new UnsupportedPlatformException(platform, ImportSourceKind.UploadedFile, SupportedPlatforms);

    public IApiImportAdapter GetApiAdapter(Platform platform) =>
        _apiAdapters.TryGetValue(platform, out var adapter)
            ? adapter
            : throw new UnsupportedPlatformException(platform, ImportSourceKind.RemoteApi, SupportedPlatforms);
}

/// <summary>
/// La plataforma pedida no tiene adaptador. El mensaje enumera las soportadas: quien
/// se equivoca de plataforma necesita saber cuáles hay, no solo que esa no vale.
/// </summary>
public sealed class UnsupportedPlatformException(
    Platform platform,
    ImportSourceKind sourceKind,
    IReadOnlyCollection<Platform> supportedPlatforms)
    : InvalidOperationException(
        $"No hay adaptador de {(sourceKind == ImportSourceKind.UploadedFile ? "fichero" : "API")} para {platform}. " +
        $"Plataformas soportadas: {string.Join(", ", supportedPlatforms)}.")
{
    public Platform Platform { get; } = platform;

    public ImportSourceKind SourceKind { get; } = sourceKind;

    public IReadOnlyCollection<Platform> SupportedPlatforms { get; } = supportedPlatforms;
}
