using ClosedXML.Excel;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Tabular;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

public class SheetMatchingTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Every_sheet_of_a_workbook_is_read()
    {
        using var book = Workbook();

        var sheets = TabularReader.ReadSheets(book, "informe.xlsx");

        Assert.Equal(["Closed Positions", "Cash Operations", "Open Positions"], sheets.Select(sheet => sheet.Name));
    }

    [Fact]
    public void The_table_is_found_below_the_report_metadata()
    {
        // La exportación de un bróker trae delante el número de cuenta, el título y el
        // periodo. Tomando la primera fila como cabecera, las columnas parecían llamarse
        // «Account number» y el fichero no lo reconocía ningún perfil.
        using var book = Workbook();
        var sheets = TabularReader.ReadSheets(book, "informe.xlsx");

        var matched = SheetMatching.Match(sheets, [Cash()], PlatformCode.Xtb);

        var cash = Assert.Single(matched);

        Assert.Equal("Cash Operations", cash.Sheet);
        Assert.Equal(5, cash.HeaderRowNumber);
        Assert.Contains("Amount", cash.Headers);

        // Y sólo entra lo que hay debajo de la cabecera.
        var rows = cash.Content(sheets.Single(sheet => sheet.Name == "Cash Operations")).Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("Deposit", rows[0].Cells[0]);
    }

    [Fact]
    public void A_profile_bound_to_a_sheet_does_not_read_another_one()
    {
        // Dos hojas con una columna en común: sin la hoja declarada, el mismo perfil
        // podría llevarse la que no le toca.
        using var book = Workbook();
        var sheets = TabularReader.ReadSheets(book, "informe.xlsx");

        var matched = SheetMatching.Match(sheets, [Cash(), ClosedPositions()], PlatformCode.Xtb);

        Assert.Equal(
            [("Closed Positions", "XTB · Posiciones cerradas"), ("Cash Operations", "XTB · Operaciones de efectivo")],
            matched.Select(sheet => (sheet.Sheet, sheet.Profile.Name)));
    }

    [Fact]
    public void A_sheet_nobody_recognises_is_left_out_without_failing()
    {
        using var book = Workbook();
        var sheets = TabularReader.ReadSheets(book, "informe.xlsx");

        var matched = SheetMatching.Match(sheets, [Cash()], PlatformCode.Xtb);

        Assert.DoesNotContain(matched, sheet => sheet.Sheet == "Open Positions");
    }

    [Fact]
    public void A_csv_keeps_working_as_a_single_nameless_sheet()
    {
        using var content = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes("Type;Time;Amount;ID\nDeposit;01/03/2026;100,00;1\n"));

        var sheet = Assert.Single(TabularReader.ReadSheets(content, "extracto.csv"));

        Assert.Equal(string.Empty, sheet.Name);
        Assert.Equal(2, sheet.Rows.Count);
    }

    /// <summary>Un libro con la forma de un informe de bróker: tres hojas y preámbulo.</summary>
    private static MemoryStream Workbook()
    {
        using var workbook = new XLWorkbook();

        var closed = workbook.Worksheets.Add("Closed Positions");
        Preamble(closed, "Closed Positions");
        Row(closed, 5, ["Instrument", "Ticker", "Type", "Volume", "Open Price", "Open Time (UTC)", "Close Price"]);
        Row(closed, 6, ["ServiceNow", "NOW.US", "BUY", "4", "136.79", "2026-05-02 10:00:00", "142.45"]);

        var cash = workbook.Worksheets.Add("Cash Operations");
        Preamble(cash, "Cash Operations");
        Row(cash, 5, ["Type", "Instrument", "Ticker", "Time", "Amount", "ID"]);
        Row(cash, 6, ["Deposit", "", "", "2026-03-01 09:00:00", "200.00", "1128939695"]);
        Row(cash, 7, ["Free funds interest", "", "", "2026-06-01 09:00:00", "0.02", "1345291313"]);

        var open = workbook.Worksheets.Add("Open Positions");
        Row(open, 1, ["Product", "Metric", "Amount", "Currency"]);
        Row(open, 2, ["My Trades", "Open position value", "0.0", "EUR"]);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return stream;
    }

    private static void Preamble(IXLWorksheet sheet, string title)
    {
        Row(sheet, 1, ["Account number", "53882396"]);
        Row(sheet, 2, [title]);
        Row(sheet, 3, ["Date from (UTC)", "2025-01-01"]);
        Row(sheet, 4, ["Date to (UTC)", "2026-09-24"]);
    }

    private static void Row(IXLWorksheet sheet, int number, string[] cells)
    {
        for (var column = 0; column < cells.Length; column++)
        {
            sheet.Cell(number, column + 1).Value = cells[column];
        }
    }

    private static ImportProfile Cash() =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Operaciones de efectivo",
            number => ImportProfileVersion.Create(
                number,
                Created,
                delimiter: ';',
                DecimalConvention.Invariant,
                "Europe/Madrid",
                ["ID", "Type", "Time", "Amount"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.NaturalId] = "ID",
                    [ImportField.Concept] = "Type",
                    [ImportField.Date] = "Time",
                    [ImportField.GrossAmount] = "Amount",
                },
                ["yyyy-MM-dd HH:mm:ss"],
                new Dictionary<string, TransactionType> { ["Deposit"] = TransactionType.Deposit },
                fixedCurrency: "EUR",
                sheet: "Cash Operations"),
            builtIn: true);

    private static ImportProfile ClosedPositions() =>
        ImportProfile.Create(
            PlatformCode.Xtb,
            "XTB · Posiciones cerradas",
            number => ImportProfileVersion.Create(
                number,
                Created,
                delimiter: ';',
                DecimalConvention.Invariant,
                "Europe/Madrid",
                ["Ticker", "Volume", "Open Price", "Open Time (UTC)"],
                new Dictionary<ImportField, string>
                {
                    [ImportField.AssetSymbol] = "Ticker",
                    [ImportField.Quantity] = "Volume",
                    [ImportField.OpenDate] = "Open Time (UTC)",
                    [ImportField.OpenPrice] = "Open Price",
                    [ImportField.CloseDate] = "Close Time (UTC)",
                    [ImportField.ClosePrice] = "Close Price",
                },
                ["yyyy-MM-dd HH:mm:ss"],
                fixedCurrency: "EUR",
                rowShape: RowShape.OpenAndClosePosition,
                sheet: "Closed Positions"),
            builtIn: true);
}
