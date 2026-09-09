using System.Text;
using ClosedXML.Excel;
using Kapea.Application.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Import.Xtb;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Import.Xtb;

public class XtbFileImportAdapterTests
{
    private static readonly XtbFileImportAdapter Adapter = new(NullLogger<XtbFileImportAdapter>.Instance);

    [Fact]
    public async Task A_cash_operations_export_is_normalised()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Stocks purchase;10.01.2024 09:30:00;SAN.ES;OPEN BUY;-1.234,56;EUR
            1002;Dividend;15.05.2024 00:00:00;SAN.ES;DIV;45,20;EUR
            """);

        Assert.Empty(result.Rejected);
        Assert.Equal(2, result.Records.Count);

        // Sin volumen no puede ser una compra: la operación completa la trae el informe
        // de posiciones, y clasificarla aquí la duplicaría con un lote de cero unidades.
        Assert.Equal(TransactionType.Unknown, result.Records[0].Type);
        Assert.Equal(1234.56m, result.Records[0].GrossAmount);
        Assert.Equal("SAN.ES", result.Records[0].AssetSymbol);
        Assert.Equal(TransactionType.Dividend, result.Records[1].Type);
        Assert.Equal(45.20m, result.Records[1].GrossAmount);
    }

    [Fact]
    public async Task Unknown_headers_reject_the_whole_file_naming_what_was_expected_and_found()
    {
        var exception = await Assert.ThrowsAsync<UnknownXtbFormatException>(() => Read("""
            Fecha;Concepto;Saldo
            10.01.2024;Algo;100,00
            """));

        Assert.Contains("Columnas esperadas", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Concepto", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Amount", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_optional_column_warns_and_keeps_importing()
    {
        var result = await Read("""
            ID;Type;Time;Amount
            1001;Deposit;10.01.2024 09:30:00;500,00
            """);

        Assert.Single(result.Records);
        Assert.Null(result.Records[0].AssetSymbol);
        Assert.Contains(result.Warnings, warning => warning.Contains("Symbol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Extra_columns_are_ignored()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency;Nueva columna
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR;lo que sea
            """);

        Assert.Single(result.Records);
        Assert.Equal(TransactionType.Deposit, result.Records[0].Type);
    }

    [Fact]
    public async Task A_european_decimal_convention_is_read_without_losing_precision()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;1.234.567,89;EUR
            """);

        Assert.Equal(1234567.89m, result.Records[0].GrossAmount);
    }

    [Fact]
    public async Task An_invariant_decimal_convention_is_also_read()
    {
        var result = await Read("""
            ID,Type,Time,Symbol,Comment,Amount,Currency
            1001,Deposit,2024-01-10 09:30:00,,,1234567.89,EUR
            """);

        Assert.Equal(1234567.89m, result.Records[0].GrossAmount);
    }

    [Fact]
    public async Task A_concept_without_equivalence_becomes_unknown()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Cashback promocional;10.01.2024 09:30:00;;;12,00;EUR
            """);

        Assert.Equal(TransactionType.Unknown, Assert.Single(result.Records).Type);
    }

    [Theory]
    [InlineData("Bonificacion inventada")]
    [InlineData("Reventa de derechos")]
    [InlineData("Compraventa asistida")]
    public async Task A_concept_that_merely_contains_a_keyword_is_not_classified_by_it(string concept)
    {
        // "inventada" contiene "venta". Con emparejamiento por subcadena, el concepto se
        // tomaba por una venta sin activo y reventaba la importación entera al confirmar.
        var result = await Read($"""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;{concept};10.01.2024 09:30:00;;;12,00;EUR
            """);

        Assert.Equal(TransactionType.Unknown, Assert.Single(result.Records).Type);
    }

    [Theory]
    [InlineData("Withholding tax", TransactionType.Dividend)]
    [InlineData("Dividendo ordinario", TransactionType.Dividend)]
    [InlineData("Ingreso de efectivo", TransactionType.Deposit)]
    public async Task A_concept_that_really_is_the_keyword_is_still_classified(string concept, TransactionType expected)
    {
        var result = await Read($"""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;{concept};10.01.2024 09:30:00;SAN.ES;;12,00;EUR
            """);

        Assert.Equal(expected, Assert.Single(result.Records).Type);
    }

    [Theory]
    [InlineData("Stocks sale")]
    [InlineData("Venta de acciones")]
    public async Task A_trade_concept_in_the_cash_report_is_left_unclassified(string concept)
    {
        // El informe de efectivo no trae volumen. Su línea de compraventa es la
        // contrapartida en dinero de una operación que el informe de posiciones ya trae.
        var result = await Read($"""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;{concept};10.01.2024 09:30:00;SAN.ES;;12,00;EUR
            """);

        Assert.Equal(TransactionType.Unknown, Assert.Single(result.Records).Type);
    }

    [Fact]
    public async Task An_informational_row_with_no_financial_effect_is_discarded_and_counted()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            1002;Account statement;11.01.2024 00:00:00;;;0,00;EUR
            """);

        Assert.Single(result.Records);
        Assert.Equal(1, result.NonFinancialRecordCount);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public async Task An_unreadable_row_is_rejected_without_stopping_the_rest()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;fecha inventada;;;500,00;EUR
            1002;Deposit;11.01.2024 09:30:00;;;600,00;EUR
            """);

        var rejected = Assert.Single(result.Rejected);

        Assert.Equal(2, rejected.RowNumber);
        Assert.Contains("fecha", rejected.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fecha inventada", rejected.RawContent, StringComparison.Ordinal);
        Assert.Equal(600m, Assert.Single(result.Records).GrossAmount);
    }

    [Fact]
    public async Task A_closed_position_becomes_a_buy_and_a_sell_with_their_own_dates()
    {
        var result = await Read("""
            Position;Symbol;Type;Volume;Open time;Open price;Close time;Close price;Commission;Currency
            77001;AAPL.US;BUY;10;05.02.2024 15:30:00;180,50;20.09.2025 16:00:00;225,75;-2,50;USD
            """);

        Assert.Equal(2, result.Records.Count);

        var buy = result.Records[0];
        var sell = result.Records[1];

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal(1805m, buy.GrossAmount);
        Assert.Equal(2.50m, buy.Fee);
        Assert.Equal(new DateTime(2024, 2, 5, 15, 30, 0), buy.NaiveOccurredAt);
        Assert.Equal(TransactionType.Sell, sell.Type);
        Assert.Equal(2257.5m, sell.GrossAmount);
        Assert.Equal(new DateTime(2025, 9, 20, 16, 0, 0), sell.NaiveOccurredAt);
        Assert.Equal(Currency.FromCode("USD"), sell.Currency);
        Assert.NotEqual(buy.NaturalId, sell.NaturalId);
    }

    [Fact]
    public async Task An_open_position_becomes_only_its_acquisition()
    {
        // El informe de cerradas nunca deja nada abierto, así que sin este formato un
        // usuario de XTB no podría tener posición alguna en la cartera.
        var result = await Read("""
            Position;Symbol;Type;Volume;Open time;Open price;Market price;Commission;Currency
            77003;SAN.ES;BUY;50;01.04.2025 09:00:00;4,10;4,55;-1,00;EUR
            """);

        var buy = Assert.Single(result.Records);

        Assert.Equal(TransactionType.Buy, buy.Type);
        Assert.Equal("SAN.ES", buy.AssetSymbol);
        Assert.Equal(50m, buy.Quantity);
        Assert.Equal(4.10m, buy.UnitPrice);
        Assert.Equal(205m, buy.GrossAmount);
        Assert.Equal(1m, buy.Fee);
        Assert.Equal(new DateTime(2025, 4, 1, 9, 0, 0), buy.NaiveOccurredAt);
    }

    [Fact]
    public async Task The_market_price_of_an_open_position_is_not_imported()
    {
        // Es el precio del momento en que se exportó el informe. Guardarlo como si
        // fuera actual daría una valoración vieja con aspecto de reciente.
        var result = await Read("""
            Position;Symbol;Type;Volume;Open time;Open price;Market price;Commission;Currency
            77003;SAN.ES;BUY;50;01.04.2025 09:00:00;4,10;9,99;-1,00;EUR
            """);

        Assert.Equal(205m, Assert.Single(result.Records).GrossAmount);
    }

    [Fact]
    public async Task An_open_and_a_closed_position_of_the_same_trade_do_not_collide()
    {
        // Las dos exportaciones usan el mismo identificador de posición. La huella los
        // distingue por el sufijo, o al cerrar la posición la compra se vería duplicada.
        var open = await Read("""
            Position;Symbol;Type;Volume;Open time;Open price;Market price;Commission;Currency
            77001;SAN.ES;BUY;100;05.02.2024 15:30:00;3,50;4,80;-2,50;EUR
            """);

        var closed = await Read("""
            Position;Symbol;Type;Volume;Open time;Open price;Close time;Close price;Commission;Currency
            77001;SAN.ES;BUY;100;05.02.2024 15:30:00;3,50;20.09.2025 16:00:00;4,80;-2,50;EUR
            """);

        Assert.Equal("77001:open", Assert.Single(open.Records).NaturalId);
        Assert.Equal("77001:open", closed.Records[0].NaturalId);
        Assert.Equal("77001:close", closed.Records[1].NaturalId);
    }

    [Fact]
    public async Task An_excel_export_is_read_like_a_csv_one()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Cash operations");
        string[] headers = ["ID", "Type", "Time", "Symbol", "Comment", "Amount", "Currency"];

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        string[] values = ["1001", "Deposit", "10.01.2024 09:30:00", string.Empty, string.Empty, "500,00", "EUR"];

        for (var column = 0; column < values.Length; column++)
        {
            sheet.Cell(2, column + 1).Value = values[column];
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = await Adapter.ReadAsync(stream, "export.xlsx");

        Assert.Equal(500m, Assert.Single(result.Records).GrossAmount);
    }

    [Fact]
    public async Task A_file_of_an_unsupported_type_is_rejected_before_being_processed()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<UnsupportedImportFileException>(
            () => Adapter.ReadAsync(stream, "extracto.pdf"));

        Assert.Contains(".xlsx", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_row_number_of_the_file_travels_with_every_record()
    {
        var result = await Read("""
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            1002;Deposit;11.01.2024 09:30:00;;;600,00;EUR
            """);

        Assert.Equal([2, 3], result.Records.Select(record => record.RowNumber));
    }

    private static async Task<ImportReadResult> Read(string csv)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        return await Adapter.ReadAsync(stream, "export.csv");
    }
}
