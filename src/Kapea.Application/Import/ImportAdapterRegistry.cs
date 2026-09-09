using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>
/// Selecciona el adaptador de API por la plataforma de la cuenta destino.
/// </summary>
/// <remarks>
/// Solo quedan aquí las plataformas que se leen por API: su firma, su paginación y sus
/// alias son código y no se deducen. Los ficheros ya no pasan por aquí, porque su
/// formato lo describe un perfil y no hace falta un adaptador por bróker.
/// </remarks>
public interface IImportAdapterRegistry
{
    IReadOnlyCollection<PlatformCode> SupportedPlatforms { get; }

    IApiImportAdapter GetApiAdapter(PlatformCode platform);

    bool Supports(PlatformCode platform);
}

public sealed class ImportAdapterRegistry : IImportAdapterRegistry
{
    private readonly Dictionary<PlatformCode, IApiImportAdapter> _apiAdapters;

    public ImportAdapterRegistry(IEnumerable<IApiImportAdapter> apiAdapters)
    {
        ArgumentNullException.ThrowIfNull(apiAdapters);

        _apiAdapters = apiAdapters.ToDictionary(adapter => adapter.Platform);
    }

    public IReadOnlyCollection<PlatformCode> SupportedPlatforms => [.. _apiAdapters.Keys.Order()];

    public bool Supports(PlatformCode platform) => _apiAdapters.ContainsKey(platform);

    public IApiImportAdapter GetApiAdapter(PlatformCode platform) =>
        _apiAdapters.TryGetValue(platform, out var adapter)
            ? adapter
            : throw new UnsupportedPlatformException(platform, ImportSourceKind.RemoteApi, SupportedPlatforms);
}

/// <summary>
/// La plataforma pedida no tiene adaptador. El mensaje enumera las soportadas: quien
/// se equivoca de plataforma necesita saber cuáles hay, no solo que esa no vale.
/// </summary>
public sealed class UnsupportedPlatformException(
    PlatformCode platform,
    ImportSourceKind sourceKind,
    IReadOnlyCollection<PlatformCode> supportedPlatforms)
    : InvalidOperationException(
        $"No hay adaptador de {(sourceKind == ImportSourceKind.UploadedFile ? "fichero" : "API")} para {platform}. " +
        $"Plataformas soportadas: {string.Join(", ", supportedPlatforms)}.")
{
    public PlatformCode Platform { get; } = platform;

    public ImportSourceKind SourceKind { get; } = sourceKind;

    public IReadOnlyCollection<PlatformCode> SupportedPlatforms { get; } = supportedPlatforms;
}
