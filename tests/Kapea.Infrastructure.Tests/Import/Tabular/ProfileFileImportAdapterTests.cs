using System.Text;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Tabular;

namespace Kapea.Infrastructure.Tests.Import.Tabular;

public class ProfileFileImportAdapterTests
{
    [Fact]
    public void European_decimals_are_read_as_the_profile_declares()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Compra;1.234,56\n",
            DecimalConvention.European);

        Assert.Equal(1234.56m, Assert.Single(result.Records).GrossAmount);
    }

    [Fact]
    public void Invariant_decimals_are_read_as_the_profile_declares()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Compra;1234.56\n",
            DecimalConvention.Invariant);

        Assert.Equal(1234.56m, Assert.Single(result.Records).GrossAmount);
    }

    [Fact]
    public void A_number_written_in_the_other_convention_is_rejected_rather_than_guessed()
    {
        // Leerlo «como se pueda» daría una cifra plausible y equivocada, que en algo
        // que acaba en una declaración es el peor resultado posible.
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Compra;1,234.56\n",
            DecimalConvention.European);

        Assert.Empty(result.Records);
        Assert.Contains("convención europea", Assert.Single(result.Rejected).Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dd/MM/yyyy", "01/03/2026")]
    [InlineData("yyyy-MM-dd", "2026-03-01")]
    [InlineData("dd.MM.yyyy HH:mm:ss", "01.03.2026 10:30:00")]
    public void Each_date_format_the_profile_declares_is_understood(string format, string value)
    {
        var result = Read(
            $"Fecha;Concepto;Importe\n{value};Compra;10,00\n",
            DecimalConvention.European,
            dateFormats: [format]);

        Assert.Equal(new DateTime(2026, 3, 1), Assert.Single(result.Records).NaiveOccurredAt!.Value.Date);
    }

    [Fact]
    public void A_date_in_no_declared_format_is_rejected_naming_the_formats()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n2026/03/01;Compra;10,00\n",
            DecimalConvention.European,
            dateFormats: ["dd/MM/yyyy"]);

        Assert.Contains("dd/MM/yyyy", Assert.Single(result.Rejected).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_concept_the_profile_translates_becomes_its_type()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Dividendo;10,00\n",
            DecimalConvention.European,
            concepts: new Dictionary<string, TransactionType> { ["Dividendo"] = TransactionType.Dividend });

        Assert.Equal(TransactionType.Dividend, Assert.Single(result.Records).Type);
    }

    [Fact]
    public void An_untranslated_concept_does_not_block_the_rest_of_the_file()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Dividendo;10,00\n02/03/2026;Bonificación rara;5,00\n",
            DecimalConvention.European,
            concepts: new Dictionary<string, TransactionType> { ["Dividendo"] = TransactionType.Dividend });

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(TransactionType.Dividend, result.Records[0].Type);
        Assert.Equal(TransactionType.Unknown, result.Records[1].Type);
        Assert.Contains(result.Warnings, warning => warning.Contains("Bonificación rara", StringComparison.Ordinal));
    }

    [Fact]
    public void The_same_untranslated_concept_is_reported_once_and_not_once_per_row()
    {
        var rows = string.Concat(Enumerable.Range(1, 20).Select(day => $"{day:00}/03/2026;Raro;1,00\n"));

        var result = Read($"Fecha;Concepto;Importe\n{rows}", DecimalConvention.European);

        Assert.Single(result.Warnings, warning => warning.Contains("Raro", StringComparison.Ordinal));
    }

    [Fact]
    public void Entries_without_financial_effect_are_counted_and_not_rejected()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Compra;10,00\n02/03/2026;Cambio de titularidad;0,00\n",
            DecimalConvention.European,
            nonFinancial: ["Cambio de titularidad"]);

        Assert.Single(result.Records);
        Assert.Equal(1, result.NonFinancialRecordCount);
        Assert.Empty(result.Rejected);
    }

    [Fact]
    public void Columns_are_matched_by_header_and_not_by_position()
    {
        // Un bróker que inserta una columna en medio desplazaría todas las demás, y
        // leyendo por posición se importaría todo corrido sin que nada fallara.
        var result = Read(
            "Concepto;Nueva;Importe;Fecha\nCompra;lo que sea;10,00;01/03/2026\n",
            DecimalConvention.European);

        var record = Assert.Single(result.Records);

        Assert.Equal(10.00m, record.GrossAmount);
        Assert.Equal(new DateTime(2026, 3, 1), record.NaiveOccurredAt!.Value.Date);
    }

    [Fact]
    public void The_currency_column_wins_over_the_fixed_one()
    {
        var result = Read(
            "Fecha;Concepto;Importe;Divisa\n01/03/2026;Compra;10,00;USD\n",
            DecimalConvention.European,
            withCurrencyColumn: true);

        Assert.Equal("USD", Assert.Single(result.Records).Currency.Code);
    }

    [Fact]
    public void The_row_number_travels_with_the_record_because_the_fingerprint_uses_it()
    {
        var result = Read(
            "Fecha;Concepto;Importe\n01/03/2026;Compra;10,00\n02/03/2026;Compra;20,00\n",
            DecimalConvention.European);

        Assert.Equal([2, 3], result.Records.Select(record => record.RowNumber));
    }

    private static Kapea.Application.Import.ImportReadResult Read(
        string csv,
        DecimalConvention convention,
        IEnumerable<string>? dateFormats = null,
        IReadOnlyDictionary<string, TransactionType>? concepts = null,
        IEnumerable<string>? nonFinancial = null,
        bool withCurrencyColumn = false)
    {
        var columns = new Dictionary<ImportField, string>
        {
            [ImportField.Date] = "Fecha",
            [ImportField.Concept] = "Concepto",
            [ImportField.GrossAmount] = "Importe",
        };

        if (withCurrencyColumn)
        {
            columns[ImportField.Currency] = "Divisa";
        }

        var version = ImportProfileVersion.Create(
            1,
            DateTimeOffset.UnixEpoch,
            delimiter: ';',
            convention,
            timeZoneId: "Europe/Madrid",
            ["Fecha", "Concepto", "Importe"],
            columns,
            dateFormats ?? ["dd/MM/yyyy", "dd.MM.yyyy HH:mm:ss", "yyyy-MM-dd"],
            concepts,
            nonFinancial,
            fixedCurrency: "EUR");

        var profile = ImportProfile.Create(PlatformCode.Xtb, "Prueba", _ => version);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var content = TabularReader.Read(stream, "extracto.csv", version.Delimiter);

        return new ProfileFileImportAdapter().Read(profile, version, content);
    }
}
