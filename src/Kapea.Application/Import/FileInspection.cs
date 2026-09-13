using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>Lo que trae un fichero, mirado sin importar nada.</summary>
public sealed record FileInspection(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    Guid? MatchedProfileId,
    string? MatchedProfileName);

/// <summary>
/// Mira un fichero para poder mapearlo.
/// </summary>
/// <remarks>
/// Va aparte de la importación porque son dos preguntas distintas: importar necesita
/// un perfil, y esto es justo lo que se hace cuando todavía no hay ninguno.
/// </remarks>
public interface IFileInspector
{
    Task<FileInspection> InspectAsync(
        PlatformCode platform,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);
}
