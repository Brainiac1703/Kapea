using ClosedXML.Excel;

namespace Kapea.Api.Tests.Import;

/// <summary>
/// Un informe de XTB inventado, con la forma del real: tres hojas, cuatro filas de
/// metadatos delante de cada tabla, y las columnas con los nombres que usa el bróker.
/// </summary>
/// <remarks>
/// Las cifras están elegidas para poder calcularlas a mano, y ninguna es de nadie. Un
/// extracto real lleva el número de cuenta y el patrimonio de una persona, así que no
/// entra en el repositorio; lo que hay que probar es que Kapea lee esta forma y calcula
/// lo que debe, y eso no necesita que los importes sean ciertos.
///
/// Reproduce además las dos trampas del informe real, porque son las que rompían:
/// el precio de una acción estadounidense viene en dólares mientras que el total ya está
/// en euros, y la hoja de efectivo repite cada compraventa con el importe en la divisa de
/// la bolsa.
/// </remarks>
internal static class FakeXtbWorkbook
{
    /// <summary>Lo ingresado desde el banco.</summary>
    internal const decimal Deposit = 1_000.00m;

    /// <summary>Una operación ganadora, comprada y vendida por completo.</summary>
    internal const decimal WinnerCost = 500.00m;
    internal const decimal WinnerProceeds = 620.00m;

    /// <summary>Y una perdedora, para que el resultado no salga de un solo signo.</summary>
    internal const decimal LoserCost = 300.00m;
    internal const decimal LoserProceeds = 240.00m;

    internal const decimal Interest = 0.50m;
    internal const decimal InterestTax = 0.10m;
    internal const decimal MarketFee = 0.05m;

    /// <summary>Lo que tiene que dar el resultado realizado: +120 y −60.</summary>
    internal static decimal ExpectedRealized => WinnerProceeds - WinnerCost + LoserProceeds - LoserCost;

    /// <summary>Y el efectivo que queda, que es lo que el bróker enseña como disponible.</summary>
    internal static decimal ExpectedCash =>
        Deposit + Interest - InterestTax - MarketFee
        - WinnerCost + WinnerProceeds - LoserCost + LoserProceeds;

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

        // Los precios van en dólares y los totales en euros. Multiplicar cantidad por
        // precio daría 580 y 720 contados como euros, y el resultado se iría un veinte
        // por ciento: por eso se toman «Purchase Value» y «Sale Value».
        Row(closed, 6,
        [
            "Acme Robotics", "ACME.US", "Stocks", "BUY", "10", "58.00", "2026-01-12 10:00:00",
            "72.00", "2026-03-18 15:30:00", "My Trades", "120.00", Cell(WinnerCost), Cell(WinnerProceeds),
            "0.0", "0.87000", "0.86000", "9000000001",
        ]);

        Row(closed, 7,
        [
            "Zephyr Industrie", "ZEPH.DE", "Stocks", "BUY", "5", "60.00", "2026-02-02 09:15:00",
            "48.00", "2026-04-09 16:45:00", "My Trades", "-60.00", Cell(LoserCost), Cell(LoserProceeds),
            "0.0", "1.00000", "1.00000", "9000000002",
        ]);

        var cash = workbook.Worksheets.Add("Cash Operations");
        Preamble(cash, "Cash Operations");
        Row(cash, 5, ["Type", "Instrument", "Ticker", "Category", "Time", "Amount", "ID", "Comment", "Product", "Position ID"]);

        Row(cash, 6, ["Deposit", "", "", "", "2026-01-05 09:00:00", Cell(Deposit), "7000000001", "Bank deposit", "My Trades", ""]);

        // Las cuatro patas que la hoja de posiciones cerradas ya trae, y con el importe
        // en dólares: contarlas otra vez duplicaría cada operación.
        Row(cash, 7, ["Stock purchase", "Acme Robotics", "ACME.US", "Stocks", "2026-01-12 10:00:00", "-580.00", "7000000002", "OPEN BUY 10", "My Trades", "9000000001"]);
        Row(cash, 8, ["Stock sell", "Acme Robotics", "ACME.US", "Stocks", "2026-03-18 15:30:00", "720.00", "7000000003", "CLOSE BUY 10", "My Trades", "9000000001"]);
        Row(cash, 9, ["Stock purchase", "Zephyr Industrie", "ZEPH.DE", "Stocks", "2026-02-02 09:15:00", "-300.00", "7000000004", "OPEN BUY 5", "My Trades", "9000000002"]);
        Row(cash, 10, ["Stock sell", "Zephyr Industrie", "ZEPH.DE", "Stocks", "2026-04-09 16:45:00", "240.00", "7000000005", "CLOSE BUY 5", "My Trades", "9000000002"]);

        Row(cash, 11, ["Free funds interest", "", "", "", "2026-05-01 09:00:00", Cell(Interest), "7000000006", "Free-funds Interest 2026-05", "My Trades", ""]);
        Row(cash, 12, ["Free funds interest tax", "", "", "", "2026-05-01 09:00:00", Cell(-InterestTax), "7000000007", "Free-funds Interest Tax 2026-05", "My Trades", ""]);
        Row(cash, 13, ["SEC fee", "Acme Robotics", "ACME.US", "Stocks", "2026-03-19 14:14:00", Cell(-MarketFee), "7000000008", "Sec Fee adj ACME.US", "My Trades", "9000000001"]);

        // El informe se despide con un total: ni fecha ni identificador, sólo la suma.
        Row(cash, 14, ["Total", "", "", "", "", Cell(ExpectedCash), "", "", "", ""]);

        // Nadie reconoce esta hoja, y está bien: son cifras de resumen, no movimientos.
        var open = workbook.Worksheets.Add("Open Positions");
        Row(open, 1, ["Account number", "10203040"]);
        Row(open, 2, ["Open Positions"]);
        Row(open, 3, ["Data as of report generated", "2026-06-30"]);
        Row(open, 4, ["Product", "Metric", "Amount", "Currency"]);
        Row(open, 5, ["My Trades", "Open position value", "0.0", "EUR"]);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return stream;
    }

    private static string Cell(decimal value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static void Preamble(IXLWorksheet sheet, string title)
    {
        Row(sheet, 1, ["Account number", "10203040"]);
        Row(sheet, 2, [title]);
        Row(sheet, 3, ["Date from (UTC)", "2025-12-31"]);
        Row(sheet, 4, ["Date to (UTC)", "2026-06-30"]);
    }

    private static void Row(IXLWorksheet sheet, int number, string[] cells)
    {
        for (var column = 0; column < cells.Length; column++)
        {
            sheet.Cell(number, column + 1).Value = cells[column];
        }
    }
}
