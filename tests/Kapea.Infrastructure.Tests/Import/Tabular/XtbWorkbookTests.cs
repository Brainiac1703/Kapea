using Kapea.Domain.Accounts;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Tabular;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

/// <summary>
/// La exportación de XTB tal y como la descarga hoy un usuario: un libro de tres hojas
/// con los metadatos del informe delante de cada tabla.
/// </summary>
/// <remarks>
/// El fichero se genera aquí en lugar de guardarlo: un extracto real lleva el número de
/// cuenta y los importes de una persona, y eso no entra en el repositorio. Lo que se
/// reproduce es su forma, que es lo que rompía.
/// </remarks>
public class XtbWorkbookTests
{
    private static readonly DateTimeOffset Created = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_current_export_is_recognised_sheet_by_sheet()
    {
        using var book = XtbWorkbook.Build();
        var sheets = TabularReader.ReadSheets(book, "EUR_10203040.xlsx");

        var matched = SheetMatching.Match(sheets, BuiltInProfiles.All(Created), PlatformCode.Xtb);

        Assert.Equal(
            [
                ("Closed Positions", "XTB · Informe · Posiciones cerradas"),
                ("Cash Operations", "XTB · Informe · Operaciones de efectivo"),
            ],
            matched.Select(sheet => (sheet.Sheet, sheet.Profile.Name)));

        // La de posiciones abiertas no la reconoce nadie, y está bien: son dos cifras de
        // resumen, no movimientos.
        Assert.DoesNotContain(matched, sheet => sheet.Sheet == "Open Positions");
    }

    [Fact]
    public void A_closed_position_becomes_a_purchase_and_a_sale_with_their_quantity()
    {
        var records = Read("Closed Positions");

        Assert.Equal(2, records.Count);

        var purchase = records.Single(record => record.Type == TransactionType.Buy);
        var sale = records.Single(record => record.Type == TransactionType.Sell);

        Assert.Equal("NOW.US", purchase.AssetSymbol);
        Assert.Equal(4m, purchase.Quantity);

        // El precio está en dólares y el total en euros: multiplicando cantidad por
        // precio salían 547,16 dólares contados como euros, y el resultado de la
        // operación se iba un veinte por ciento.
        Assert.Equal(474.67m, purchase.GrossAmount);
        Assert.Equal(490.58m, sale.GrossAmount);
        Assert.Equal(15.91m, sale.GrossAmount - purchase.GrossAmount);
    }

    [Fact]
    public void Cash_operations_bring_what_no_other_sheet_brings()
    {
        var records = Read("Cash Operations");

        Assert.Equal(TransactionType.Deposit, records.Single(record => record.NaturalId == "1128939695").Type);
        Assert.Equal(TransactionType.Interest, records.Single(record => record.NaturalId == "1345291313").Type);

        // La retención del interés y la comisión son gasto: el dinero sale.
        Assert.Equal(TransactionType.Fee, records.Single(record => record.NaturalId == "1345125458").Type);
        Assert.Equal(TransactionType.Fee, records.Single(record => record.NaturalId == "1439699677").Type);
    }

    [Fact]
    public void What_the_positions_sheet_already_brings_is_not_counted_again()
    {
        // Una compra aparece en las dos hojas: con su cantidad en la de posiciones y sólo
        // como importe en la de efectivo. Importar las dos duplicaría el dinero.
        var read = ReadSheet("Cash Operations");

        Assert.DoesNotContain(read.Records, record => record.NaturalId == "1444973238");
        Assert.True(read.NonFinancialRecordCount >= 2);
    }

    [Fact]
    public void The_total_the_report_ends_with_is_not_a_movement()
    {
        // Ni fecha ni identificador: es la suma de lo de arriba. Antes entraba como
        // registro rechazado, que se lee como un problema y no lo hay.
        var read = ReadSheet("Cash Operations");

        Assert.Empty(read.Rejected);
        Assert.DoesNotContain(read.Records, record => record.GrossAmount == 603.76m);
    }

    private static IReadOnlyList<Kapea.Application.Import.ImportRecord> Read(string sheetName) =>
        ReadSheet(sheetName).Records;

    private static Kapea.Application.Import.ImportReadResult ReadSheet(string sheetName)
    {
        using var book = XtbWorkbook.Build();
        var sheets = TabularReader.ReadSheets(book, "EUR_10203040.xlsx");
        var matched = SheetMatching.Match(sheets, BuiltInProfiles.All(Created), PlatformCode.Xtb)
            .Single(sheet => sheet.Sheet == sheetName);

        return new ProfileFileImportAdapter().Read(
            matched.Profile,
            matched.Profile.Current,
            matched.Content(sheets.Single(sheet => sheet.Name == sheetName)),
            CancellationToken.None);
    }
}
