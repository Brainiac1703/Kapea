using Kapea.Application.Import;
using Kapea.Domain.Accounts;

namespace Kapea.Application.Tests.Import;

public class ImportFingerprintTests
{
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid OtherAccount = Guid.NewGuid();

    [Fact]
    public void The_same_source_record_always_yields_the_same_fingerprint()
    {
        var record = ImportRecords.Buy(naturalId: "TXID-1");

        Assert.Equal(
            ImportFingerprint.For(Account, Platform.Kraken, record),
            ImportFingerprint.For(Account, Platform.Kraken, record));
    }

    [Fact]
    public void The_natural_id_is_preferred_when_the_source_provides_one()
    {
        // Con identificador natural, reordenar el fichero o cambiar un dato accesorio
        // no altera la huella: el registro sigue siendo el mismo.
        var first = ImportRecords.Buy(naturalId: "TXID-1", rowNumber: 3, grossAmount: 1000m);
        var second = ImportRecords.Buy(naturalId: "TXID-1", rowNumber: 47, grossAmount: 1000m);

        Assert.True(ImportFingerprint.UsesNaturalId(first));
        Assert.Equal(
            ImportFingerprint.For(Account, Platform.Kraken, first),
            ImportFingerprint.For(Account, Platform.Kraken, second));
    }

    [Fact]
    public void Without_a_natural_id_the_financial_data_and_the_row_number_are_used()
    {
        var record = ImportRecords.Buy(rowNumber: 3);

        Assert.False(ImportFingerprint.UsesNaturalId(record));
        Assert.NotEqual(
            ImportFingerprint.For(Account, Platform.Xtb, record),
            ImportFingerprint.For(Account, Platform.Xtb, ImportRecords.Buy(rowNumber: 4)));
    }

    [Fact]
    public void Two_identical_operations_on_different_rows_keep_different_fingerprints()
    {
        // Este es el caso que justifica meter la fila en la huella: dos operaciones
        // legítimas idénticas en el mismo instante no pueden colapsar en una.
        var first = ImportRecords.Buy(rowNumber: 12);
        var second = ImportRecords.Buy(rowNumber: 13);

        Assert.NotEqual(
            ImportFingerprint.For(Account, Platform.Xtb, first),
            ImportFingerprint.For(Account, Platform.Xtb, second));
    }

    [Fact]
    public void The_same_record_in_another_account_is_a_different_record()
    {
        var record = ImportRecords.Buy(naturalId: "TXID-1");

        Assert.NotEqual(
            ImportFingerprint.For(Account, Platform.Kraken, record),
            ImportFingerprint.For(OtherAccount, Platform.Kraken, record));
    }

    [Theory]
    [InlineData("11", "1000", "2024-01-10")]
    [InlineData("10", "1001", "2024-01-10")]
    [InlineData("10", "1000", "2024-01-11")]
    public void Any_change_in_the_financial_data_changes_the_fingerprint(string quantityText, string grossText, string date)
    {
        var quantity = decimal.Parse(quantityText, System.Globalization.CultureInfo.InvariantCulture);
        var gross = decimal.Parse(grossText, System.Globalization.CultureInfo.InvariantCulture);

        var baseline = ImportFingerprint.For(Account, Platform.Xtb, ImportRecords.Buy(rowNumber: 1));

        var changed = ImportFingerprint.For(
            Account, Platform.Xtb, ImportRecords.Buy(rowNumber: 1, quantity: quantity, grossAmount: gross, date: date));

        Assert.NotEqual(baseline, changed);
    }
}
