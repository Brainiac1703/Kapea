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

        var sheets = TabularReader.ReadSheets(content, fileName);

        var profiles = await context.ImportProfiles
            .Where(profile => profile.Platform == platform)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Se enseña la primera hoja que algún perfil reconoce. Si ninguna lo es, la
        // primera del fichero: es lo que el usuario tiene delante para decidir qué
        // columnas significan qué.
        var matched = SheetMatching.Match(sheets, profiles, platform).FirstOrDefault();

        var tabular = matched is null
            ? Preview(sheets)
            : matched.Content(sheets.First(sheet => sheet.Name == matched.Sheet));

        var match = matched?.Match ?? ProfileMatch.None;

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

    /// <summary>Lo que se enseña cuando ningún perfil reconoce nada: la primera hoja, tal cual.</summary>
    private static TabularContent Preview(IReadOnlyList<TabularSheet> sheets)
    {
        var first = sheets.FirstOrDefault(sheet => sheet.Rows.Count > 0);

        return first is null
            ? new TabularContent([], [])
            : new TabularContent(first.Rows[0].Cells, [.. first.Rows.Skip(1)], first.Name);
    }
}
