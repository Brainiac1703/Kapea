using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;

namespace Kapea.Infrastructure.Import.Xtb;

/// <summary>Contenido tabular de un fichero subido: cabeceras y filas, ya como texto.</summary>
public sealed record TabularContent(IReadOnlyList<string> Headers, IReadOnlyList<TabularRow> Rows);

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

    public static TabularContent Read(Stream content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);

        return Extension(fileName) switch
        {
            ".xlsx" or ".xlsm" => ReadExcel(content),
            ".csv" or ".txt" => ReadCsv(content),
            var extension => throw new UnsupportedImportFileException(extension),
        };
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

    private static TabularContent ReadCsv(Stream content)
    {
        // XTB exporta CSV con punto y coma cuando la cuenta está en convención europea,
        // porque la coma ya está ocupada como separador decimal. Se detecta en lugar de
        // asumirlo: la misma cuenta puede exportar de las dos formas.
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        var firstLine = text.Split('\n').FirstOrDefault() ?? string.Empty;
        var delimiter = firstLine.Count(character => character == ';') > firstLine.Count(character => character == ',')
            ? ";"
            : ",";

        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var csv = new CsvReader(new StringReader(text), configuration);
        var headers = new List<string>();
        var rows = new List<TabularRow>();
        var number = 0;

        while (csv.Read())
        {
            number++;
            var cells = Enumerable.Range(0, csv.Parser.Count).Select(index => csv.GetField(index) ?? string.Empty).ToList();

            if (number == 1)
            {
                headers = cells;
                continue;
            }

            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new TabularRow(number, cells));
        }

        return new TabularContent(headers, rows);
    }
}

/// <summary>El fichero subido no es de un tipo que se sepa leer.</summary>
public sealed class UnsupportedImportFileException(string extension)
    : InvalidOperationException(
        $"No se admiten ficheros '{extension}'. Sube la exportación en Excel (.xlsx) o CSV (.csv).");
