using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>Lo leído de un fichero y con qué reglas se leyó.</summary>
/// <param name="Ambiguous">Varios perfiles reconocían el fichero. Se usó uno, y conviene decirlo.</param>
public sealed record FileImportResult(
    ImportReadResult Read,
    Guid ProfileId,
    int ProfileVersion,
    string ProfileName,
    bool Ambiguous);

/// <summary>
/// Lee un fichero eligiendo el perfil que sabe interpretarlo.
/// </summary>
/// <remarks>
/// Sustituye a tener un adaptador por plataforma. Ya no hay nada específico de un
/// bróker en el código: el formato lo describe un perfil, y elegirlo es cuestión de
/// mirar las cabeceras.
/// </remarks>
public interface IFileImporter
{
    Task<FileImportResult> ReadAsync(
        PlatformCode platform,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Ningún perfil de la plataforma reconoce las cabeceras del fichero.
/// </summary>
/// <remarks>
/// El mensaje dice lo encontrado y lo esperado porque es exactamente lo que hace falta
/// para dar de alta el perfil que falta, que ahora es algo que se hace desde la
/// aplicación y no reescribiendo código.
/// </remarks>
public sealed class UnknownFileFormatException(
    PlatformCode platform,
    IReadOnlyList<string> foundHeaders,
    IReadOnlyList<string> knownProfiles)
    : InvalidOperationException(BuildMessage(platform, foundHeaders, knownProfiles))
{
    public IReadOnlyList<string> FoundHeaders { get; } = foundHeaders;

    private static string BuildMessage(
        PlatformCode platform,
        IReadOnlyList<string> foundHeaders,
        IReadOnlyList<string> knownProfiles) =>
        $"Ningún perfil de {platform} reconoce este fichero. " +
        $"Columnas encontradas: {(foundHeaders.Count == 0 ? "ninguna" : string.Join(", ", foundHeaders))}. " +
        $"Perfiles configurados: {(knownProfiles.Count == 0 ? "ninguno" : string.Join(", ", knownProfiles))}.";
}
