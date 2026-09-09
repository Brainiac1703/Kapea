using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>Lee un fichero con el perfil de la plataforma que reconozca sus cabeceras.</summary>
public sealed class FileImporter(
    KapeaDbContext context,
    ILogger<FileImporter> logger) : IFileImporter
{
    public async Task<FileImportResult> ReadAsync(
        PlatformCode platform,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!TabularReader.IsSupported(fileName))
        {
            throw new UnsupportedImportFileException(Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant());
        }

        var profiles = await context.ImportProfiles
            .Where(profile => profile.Platform == platform)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Primero se lee dejando que el delimitador se deduzca, porque hasta conocer el
        // perfil no se sabe cuál declara. Las cabeceras salen igual con cualquiera de
        // los dos separadores habituales.
        var probe = TabularReader.Read(content, fileName);
        var match = ProfileMatching.Match(profiles, platform, probe.Headers);

        if (!match.Found)
        {
            throw new UnknownFileFormatException(
                platform, probe.Headers, [.. profiles.Select(profile => profile.Name)]);
        }

        var profile = match.Profile!;
        var version = profile.Current;

        if (match.Ambiguous)
        {
            logger.LogInformation(
                "Varios perfiles reconocían el fichero; se ha usado '{Perfil}' por ser el más específico.",
                profile.Name);
        }

        // Segunda lectura con el delimitador que declara el perfil: la primera solo
        // servía para saber cuál era.
        content.Position = 0;
        var tabular = TabularReader.Read(content, fileName, version.Delimiter);

        logger.LogInformation(
            "Fichero leído con el perfil '{Perfil}' versión {Version}: {Filas} filas.",
            profile.Name, version.Number, tabular.Rows.Count);

        var read = new ProfileFileImportAdapter().Read(profile, version, tabular, cancellationToken);

        if (match.Ambiguous)
        {
            read = read with
            {
                Warnings =
                [
                    .. read.Warnings,
                    $"Varios perfiles reconocían este fichero. Se ha usado '{profile.Name}'.",
                ],
            };
        }

        return new FileImportResult(read, profile.Id, version.Number, profile.Name, match.Ambiguous);
    }
}
