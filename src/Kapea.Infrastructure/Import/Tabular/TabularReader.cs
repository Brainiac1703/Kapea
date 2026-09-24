using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>Contenido tabular de un fichero subido: cabeceras y filas, ya como texto.</summary>
/// <param name="Sheet">Hoja de la que sale. Vacío en un fichero que no tiene hojas.</param>
public sealed record TabularContent(
    IReadOnlyList<string> Headers,
    IReadOnlyList<TabularRow> Rows,
    string Sheet = "");

/// <summary>
/// Una hoja tal cual está en el fichero, sin decidir todavía dónde empieza su tabla.
/// </summary>
/// <remarks>
/// El lector entrega filas y no interpreta: un informe puede traer sus metadatos
/// delante, y cuál es la fila de cabeceras lo sabe quien conoce los perfiles, no quien
/// abre el fichero.
/// </remarks>
public sealed record TabularSheet(string Name, IReadOnlyList<TabularRow> Rows);

/// <summary>Fila del fichero con su número real, que se conserva porque entra en la huella de deduplicación.</summary>
public sealed record TabularRow(int Number, IReadOnlyList<string> Cells)
{
    public string? Cell(int index) => index >= 0 && index < Cells.Count ? Cells[index] : null;

    public string Raw => string.Join(" | ", Cells);
}

/// <summary>
/// Lee Excel y CSV a una misma representación. El resto del adaptador trabaja solo
/// contra esta forma, así que el formato del contenedor deja de importar.
/// </summary>
public static class TabularReader
{
    public static bool IsSupported(string fileName) =>
        Extension(fileName) is ".csv" or ".xlsx" or ".xlsm" or ".txt";

    /// <param name="delimiter">
    /// Separador que declara el perfil. Nulo para deducirlo del propio fichero, que es
    /// lo que hay que hacer cuando todavía no se sabe con qué perfil se va a leer.
    /// </param>
    public static TabularContent Read(Stream content, string fileName, char? delimiter = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return Extension(fileName) switch
        {
            ".xlsx" or ".xlsm" => ReadExcel(content),
            ".csv" or ".txt" => ReadCsv(content, delimiter),
            var extension => throw new UnsupportedImportFileException(extension),
        };
    }

    /// <summary>
    /// Todas las hojas del fichero con sus filas en crudo, para buscar en ellas.
    /// </summary>
    /// <remarks>
    /// Un CSV no tiene hojas y devuelve una sola, sin nombre. Así quien busca la tabla
    /// no necesita saber de qué formato venía.
    /// </remarks>
    public static IReadOnlyList<TabularSheet> ReadSheets(Stream content, string fileName, char? delimiter = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        return Extension(fileName) switch
        {
            ".xlsx" or ".xlsm" => ExcelSheets(content),
            ".csv" or ".txt" => [new TabularSheet(string.Empty, AllCsvRows(content, delimiter))],
            var extension => throw new UnsupportedImportFileException(extension),
        };
    }

    private static IReadOnlyList<TabularSheet> ExcelSheets(Stream content)
    {
        using var workbook = new XLWorkbook(content);

        return
        [
            .. workbook.Worksheets.Select(sheet => new TabularSheet(
                sheet.Name,
                sheet.RangeUsed() is not { } used
                    ? []
                    : [.. used.RowsUsed().Select(row => new TabularRow(
                        row.RowNumber(),
                        [.. row.Cells(1, row.LastCellUsed()?.Address.ColumnNumber ?? 1)
                            .Select(cell => cell.GetFormattedString())]))])),
        ];
    }

    private static string Extension(string fileName) =>
        Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

    private static TabularContent ReadExcel(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();

        if (used is null)
        {
            return new TabularContent([], []);
        }

        var rows = used.RowsUsed().ToList();
        var headers = rows[0].Cells().Select(cell => cell.GetFormattedString()).ToList();

        var data = rows.Skip(1)
            .Select(row => new TabularRow(
                row.RowNumber(),
                row.Cells(1, headers.Count).Select(cell => cell.GetFormattedString()).ToList()))
            .ToList();

        return new TabularContent(headers, data);
    }

    private static TabularContent ReadCsv(Stream content, char? declared)
    {
        var rows = AllCsvRows(content, declared);

        if (rows.Count == 0)
        {
            return new TabularContent([], []);
        }

        return new TabularContent(
            rows[0].Cells,
            [.. rows.Skip(1).Where(row => !row.Cells.All(string.IsNullOrWhiteSpace))]);
    }

    private static List<TabularRow> AllCsvRows(Stream content, char? declared)
    {
        // Un extracto europeo suele venir con punto y coma, porque la coma ya está
        // ocupada como separador decimal. Se deduce solo cuando nadie lo ha declarado:
        // la misma cuenta puede exportar de las dos formas.
        // El flujo se deja abierto: el mismo fichero se lee dos veces, una para ver sus
        // cabeceras y elegir el perfil y otra con el delimitador que ese perfil declara.
        using var reader = new StreamReader(
            content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        var firstLine = text.Split('\n').FirstOrDefault() ?? string.Empty;
        var delimiter = declared?.ToString()
            ?? (firstLine.Count(character => character == ';') > firstLine.Count(character => character == ',')
                ? ";"
                : ",");

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var csv = new CsvReader(new StringReader(text), configuration);
        var rows = new List<TabularRow>();
        var number = 0;

        while (csv.Read())
        {
            number++;
            rows.Add(new TabularRow(
                number,
                [.. Enumerable.Range(0, csv.Parser.Count).Select(index => csv.GetField(index) ?? string.Empty)]));
        }

        return rows;
    }
}

/// <summary>El fichero subido no es de un tipo que se sepa leer.</summary>
public sealed class UnsupportedImportFileException(string extension)
    : InvalidOperationException(
        $"No se admiten ficheros '{extension}'. Sube la exportación en Excel (.xlsx) o CSV (.csv).");
