using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>Lee las cabeceras y unas pocas filas, y dice si algún perfil las reconoce.</summary>
public sealed class FileInspector(KapeaDbContext context) : IFileInspector
{
    public async Task<FileInspection> InspectAsync(
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

        var tabular = TabularReader.Read(content, fileName);

        var profiles = await context.ImportProfiles
            .Where(profile => profile.Platform == platform)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var match = ProfileMatching.Match(profiles, platform, tabular.Headers);

        // Solo las que se pueden llegar a enviar. Guardar más aquí para «por si acaso»
        // sería exactamente lo que la garantía dice que no ocurre.
        var sample = tabular.Rows
            .Take(MappingSample.MaximumRows)
            .Select(row => (IReadOnlyList<string>)[.. row.Cells])
            .ToList();

        return new FileInspection(
            tabular.Headers,
            sample,
            match.Profile?.Id,
            match.Profile?.Name);
    }
}
