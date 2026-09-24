using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>Una hoja reconocida: dónde empieza su tabla y con qué perfil se lee.</summary>
/// <param name="HeaderRowNumber">Fila real del fichero donde están las cabeceras.</param>
public sealed record MatchedSheet(
    string Sheet,
    int HeaderRowNumber,
    IReadOnlyList<string> Headers,
    ProfileMatch Match)
{
    public ImportProfile Profile => Match.Profile!;

    /// <summary>Lo que hay debajo de la cabecera, que es lo que se importa.</summary>
    public TabularContent Content(TabularSheet sheet) =>
        new(
            Headers,
            [.. sheet.Rows
                .Where(row => row.Number > HeaderRowNumber && !row.Cells.All(string.IsNullOrWhiteSpace))],
            Sheet);
}

/// <summary>
/// Busca en un fichero dónde empieza cada tabla y qué perfil la reconoce.
/// </summary>
/// <remarks>
/// Un informe puede traer metadatos delante —número de cuenta, título, periodo— y
/// repartir lo suyo en varias hojas. La fila de cabeceras es la primera que un perfil
/// reconoce, y no la primera con aspecto de cabecera: así el criterio es un dato que el
/// usuario ve y puede corregir, y no una adivinanza enterrada en el código.
/// </remarks>
public static class SheetMatching
{
    /// <summary>
    /// Hasta dónde se busca la cabecera. Un preámbulo son unas pocas filas; seguir
    /// bajando por una hoja de miles sólo encontraría coincidencias por casualidad.
    /// </summary>
    private const int MaximumPreambleRows = 25;

    public static IReadOnlyList<MatchedSheet> Match(
        IReadOnlyList<TabularSheet> sheets,
        IReadOnlyCollection<ImportProfile> profiles,
        PlatformCode platform)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(profiles);

        return [.. sheets.Select(sheet => Match(sheet, profiles, platform)).OfType<MatchedSheet>()];
    }

    private static MatchedSheet? Match(
        TabularSheet sheet,
        IReadOnlyCollection<ImportProfile> profiles,
        PlatformCode platform)
    {
        // Un perfil atado a una hoja sólo se aplica a la suya: es lo que evita que dos
        // hojas de cabeceras parecidas se lean con el perfil de la otra.
        var eligible = profiles
            .Where(profile => profile.Current.Sheet is not { Length: > 0 } declared
                || declared.Equals(sheet.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (eligible.Count == 0)
        {
            return null;
        }

        foreach (var row in sheet.Rows.Take(MaximumPreambleRows))
        {
            var headers = row.Cells;
            var match = ProfileMatching.Match(eligible, platform, headers);

            if (match.Found)
            {
                return new MatchedSheet(sheet.Name, row.Number, headers, match);
            }
        }

        return null;
    }
}
