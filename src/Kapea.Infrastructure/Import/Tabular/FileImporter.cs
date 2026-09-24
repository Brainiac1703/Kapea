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

        // Primero se leen las hojas en crudo, dejando que el delimitador se deduzca:
        // hasta conocer el perfil no se sabe cuál declara, y las cabeceras salen igual
        // con cualquiera de los dos separadores habituales.
        var sheets = TabularReader.ReadSheets(content, fileName);
        var matched = SheetMatching.Match(sheets, profiles, platform);

        if (matched.Count == 0)
        {
            throw new UnknownFileFormatException(
                platform,
                [.. sheets.SelectMany(sheet => sheet.Rows.Take(1)).SelectMany(row => row.Cells)],
                [.. profiles.Select(profile => profile.Name)]);
        }

        var records = new List<ImportRecord>();
        var rejected = new List<RejectedRecord>();
        var withoutEffect = 0;
        var warnings = new List<string>();

        foreach (var sheet in matched)
        {
            var profile = sheet.Profile;
            var version = profile.Current;

            // Segunda lectura con el delimitador que declara el perfil: la primera solo
            // servía para saber cuál era.
            content.Position = 0;
            var reread = TabularReader.ReadSheets(content, fileName, version.Delimiter)
                .First(candidate => candidate.Name == sheet.Sheet);

            var read = new ProfileFileImportAdapter()
                .Read(profile, version, sheet.Content(reread), cancellationToken);

            // Cada registro se lleva con qué perfil y de qué hoja salió: la ejecución es
            // una sola, pero sus movimientos no tienen por qué venir del mismo sitio.
            records.AddRange(read.Records.Select(record => record with
            {
                Sheet = string.IsNullOrEmpty(sheet.Sheet) ? null : sheet.Sheet,
                ProfileId = profile.Id,
                ProfileVersion = version.Number,
            }));

            rejected.AddRange(read.Rejected);
            withoutEffect += read.NonFinancialRecordCount;
            warnings.AddRange(read.Warnings);

            if (sheet.Match.Ambiguous)
            {
                logger.LogInformation(
                    "Varios perfiles reconocían la hoja '{Hoja}'; se ha usado '{Perfil}' por ser el más específico.",
                    sheet.Sheet, profile.Name);

                warnings.Add(Ambiguity(sheet, profile.Name));
            }

            logger.LogInformation(
                "Hoja '{Hoja}' leída con el perfil '{Perfil}' versión {Version}: {Filas} filas.",
                sheet.Sheet, profile.Name, version.Number, read.Records.Count);
        }

        var ignored = sheets.Count - matched.Count;

        if (ignored > 0)
        {
            // Una hoja que nadie reconoce no es un error: un informe trae resúmenes que
            // no son movimientos. Pero se dice, porque callarlo es indistinguible de
            // haberla perdido.
            warnings.Add(Ignored(sheets, matched));
        }

        var main = matched[0];

        return new FileImportResult(
            new ImportReadResult(records, rejected, withoutEffect) { Warnings = warnings },
            main.Profile.Id,
            main.Profile.Current.Number,
            main.Profile.Name,
            matched.Any(sheet => sheet.Match.Ambiguous));
    }

    private static string Ambiguity(MatchedSheet sheet, string profileName) =>
        string.IsNullOrEmpty(sheet.Sheet)
            ? $"Varios perfiles reconocían este fichero. Se ha usado '{profileName}'."
            : $"Varios perfiles reconocían la hoja '{sheet.Sheet}'. Se ha usado '{profileName}'.";

    private static string Ignored(IReadOnlyList<TabularSheet> sheets, IReadOnlyList<MatchedSheet> matched)
    {
        var names = sheets
            .Where(sheet => !matched.Any(read => read.Sheet == sheet.Name))
            .Select(sheet => $"'{sheet.Name}'");

        return $"No se ha leído {string.Join(", ", names)}: ningún perfil reconoce sus columnas.";
    }
}
