using ClosedXML.Excel;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

/// <summary>
/// Reproduce la forma de la exportación de XTB: tres hojas, cuatro filas de metadatos
/// delante de cada tabla, y las columnas con los nombres que usa hoy.
/// </summary>
internal static class XtbWorkbook
{
    internal static MemoryStream Build()
    {
        using var workbook = new XLWorkbook();

        var closed = workbook.Worksheets.Add("Closed Positions");
        Preamble(closed, "Closed Positions");
        Row(closed, 5,
        [
            "Instrument", "Ticker", "Category", "Type", "Volume", "Open Price", "Open Time (UTC)",
            "Close Price", "Close Time (UTC)", "Product", "Profit/Loss", "Purchase Value", "Sale Value",
            "Commission", "Open Conversion Rate", "Close Conversion Rate", "Position ID",
        ]);

        // Una acción estadounidense: el precio va en dólares y el total en euros, ya
        // convertido con el cambio que aplicó el bróker.
        Row(closed, 6,
        [
            "ServiceNow", "NOW.US", "Stocks", "BUY", "4", "136.79", "2026-05-02 10:00:00",
            "142.45", "2026-06-11 15:30:00", "My Trades", "15.91", "474.67", "490.58",
            "0.0", "0.86751", "0.86096", "2787702767",
        ]);

        var cash = workbook.Worksheets.Add("Cash Operations");
        Preamble(cash, "Cash Operations");
        Row(cash, 5, ["Type", "Instrument", "Ticker", "Category", "Time", "Amount", "ID", "Comment", "Product", "Position ID"]);
        Row(cash, 6, ["Stock sell", "ServiceNow", "NOW.US", "Stocks", "2026-06-11 15:30:00", "569.80", "1444973238", "CLOSE BUY 4", "My Trades", "2787702767"]);
        Row(cash, 7, ["Stock purchase", "ServiceNow", "NOW.US", "Stocks", "2026-05-02 10:00:00", "-547.16", "1444973200", "OPEN BUY 4", "My Trades", "2787702767"]);
        Row(cash, 8, ["Deposit", "", "", "", "2026-03-01 09:00:00", "200.0", "1128939695", "PayPal deposit", "My Trades", ""]);
        Row(cash, 9, ["Free funds interest", "", "", "", "2026-06-01 09:00:00", "0.02", "1345291313", "Free-funds Interest 2026-06", "My Trades", ""]);
        Row(cash, 10, ["Free funds interest tax", "", "", "", "2026-06-01 09:00:00", "-0.01", "1345125458", "Free-funds Interest Tax 2026-06", "My Trades", ""]);
        Row(cash, 11, ["SEC fee", "ServiceNow", "NOW.US", "Stocks", "2026-09-14 14:14:00", "-0.02", "1439699677", "Sec Fee adj NOW.US", "My Trades", "2793382390"]);

        // El informe se despide con un total: ni fecha ni identificador, sólo la suma.
        Row(cash, 12, ["Total", "", "", "", "", "603.76", "", "", "", ""]);

        var open = workbook.Worksheets.Add("Open Positions");
        Row(open, 1, ["Account number", "53882396"]);
        Row(open, 2, ["Open Positions"]);
        Row(open, 3, ["Data as of report generated", "2026-09-24"]);
        Row(open, 4, ["Product", "Metric", "Amount", "Currency"]);
        Row(open, 5, ["My Trades", "Open position value", "0.0", "EUR"]);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return stream;
    }

    private static void Preamble(IXLWorksheet sheet, string title)
    {
        Row(sheet, 1, ["Account number", "53882396"]);
        Row(sheet, 2, [title]);
        Row(sheet, 3, ["Date from (UTC)", "2024-12-31"]);
        Row(sheet, 4, ["Date to (UTC)", "2026-09-24"]);
    }

    private static void Row(IXLWorksheet sheet, int number, string[] cells)
    {
        for (var column = 0; column < cells.Length; column++)
        {
            sheet.Cell(number, column + 1).Value = cells[column];
        }
    }
}
