using System.Reflection;
using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Transactions;

public class TransactionTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly Guid ImportRun = Guid.NewGuid();

    [Fact]
    public void Financial_data_of_a_transaction_has_no_public_setters()
    {
        // La inmutabilidad no se puede probar intentando mutar: no compilaría. Lo que se
        // comprueba es que ningún llamante tiene vía para hacerlo. Los mutadores privados
        // existen solo porque EF Core no sabe enlazar tipos complejos por constructor.
        var financialProperties = new[]
        {
            nameof(Transaction.Quantity), nameof(Transaction.UnitPrice), nameof(Transaction.GrossAmount),
            nameof(Transaction.Fee), nameof(Transaction.WithholdingTax), nameof(Transaction.OccurredAt),
            nameof(Transaction.Type), nameof(Transaction.AssetId), nameof(Transaction.AccountId),
            nameof(Transaction.Source),
        };

        foreach (var name in financialProperties)
        {
            var property = typeof(Transaction).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(property);
            Assert.True(
                property!.SetMethod is null or { IsPublic: false },
                $"La propiedad {name} expone un mutador público.");
        }
    }

    [Fact]
    public void An_imported_transaction_must_reference_its_import_run()
    {
        var exception = Assert.Throws<DomainException>(() => Transaction.Imported(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(1m), Money.Euros(10m), Money.Euros(10m),
            Money.Euros(0m), Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"),
            new TransactionSource(ImportRunId: null, NaturalId: "x", RowNumber: null, Fingerprint: "f")));

        Assert.Contains("ejecución de importación", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_buy_without_asset_is_rejected() =>
        Assert.Throws<DomainException>(() => Transaction.Imported(
            Owner, Account, TransactionType.Buy, assetId: null, new Quantity(10m), Money.Euros(1m),
            Money.Euros(10m), Money.Euros(0m), Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"),
            TransactionSource.FromImport(ImportRun, "txid-1", rowNumber: null, "fingerprint")));

    [Fact]
    public void A_buy_without_quantity_is_rejected() =>
        Assert.Throws<DomainException>(() => Buy(quantity: Quantity.Zero));

    [Fact]
    public void A_fee_in_another_currency_than_the_operation_is_rejected() =>
        Assert.Throws<CurrencyMismatchException>(() => Buy(fee: new Money(1m, Currency.FromCode("USD"))));

    [Fact]
    public void An_unknown_type_marks_the_transaction_for_review()
    {
        var transaction = Transaction.Imported(
            Owner, Account, TransactionType.Unknown, assetId: null, Quantity.Zero, unitPrice: null,
            Money.Euros(0m), Money.Euros(0m), Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"),
            TransactionSource.FromImport(ImportRun, "raw-1", null, "fingerprint"));

        Assert.True(transaction.RequiresReview);
    }

    [Fact]
    public void A_naive_date_is_stored_in_utc_with_its_source_time_zone()
    {
        // Las 10:00 de un día de verano en Madrid son las 08:00 UTC. Sin conservar la
        // zona no habría forma de volver a la fecha que el usuario vio en su bróker.
        var occurrence = Occurrence.FromNaive(new DateTime(2026, 7, 15, 10, 0, 0), "Europe/Madrid");

        Assert.Equal(TimeSpan.Zero, occurrence.Instant.Offset);
        Assert.Equal(new DateTime(2026, 7, 15, 8, 0, 0), occurrence.Instant.UtcDateTime);
        Assert.Equal("Europe/Madrid", occurrence.SourceTimeZoneId);
        Assert.Equal(10, occurrence.InSourceTimeZone.Hour);
    }

    [Fact]
    public void A_local_kind_date_is_rejected_because_the_server_zone_is_not_the_source_zone() =>
        Assert.Throws<DomainException>(
            () => Occurrence.FromNaive(DateTime.SpecifyKind(new DateTime(2026, 7, 15), DateTimeKind.Local), "Europe/Madrid"));

    [Fact]
    public void An_unknown_time_zone_is_rejected() =>
        Assert.Throws<DomainException>(() => Occurrence.FromNaive(new DateTime(2026, 7, 15), "Mars/Olympus"));

    [Fact]
    public void An_imported_transaction_traces_back_to_its_source_record()
    {
        var transaction = Buy();

        Assert.Equal(TransactionOrigin.Imported, transaction.Origin);
        Assert.Equal(ImportRun, transaction.Source.ImportRunId);
        Assert.Equal("txid-1", transaction.Source.NaturalId);
        Assert.Null(transaction.AdjustmentReason);
    }

    [Fact]
    public void A_manual_adjustment_is_distinguishable_from_an_imported_transaction()
    {
        var adjustment = ManualAdjustment.Create(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(1m), Money.Euros(10m), Money.Euros(10m),
            Money.Euros(0m), Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"),
            "Compra de 2019 que XTB no exporta", DateTimeOffset.UtcNow);

        Assert.Equal(TransactionOrigin.ManualAdjustment, adjustment.Transaction.Origin);
        Assert.Equal("Compra de 2019 que XTB no exporta", adjustment.Transaction.AdjustmentReason);
        Assert.Null(adjustment.Transaction.Source.ImportRunId);
        Assert.StartsWith("manual:", adjustment.Transaction.Source.Fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_manual_adjustment_without_reason_is_rejected() =>
        Assert.Throws<DomainException>(() => ManualAdjustment.Create(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(1m), Money.Euros(10m), Money.Euros(10m),
            Money.Euros(0m), Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"), "   ", DateTimeOffset.UtcNow));

    private static Transaction Buy(Quantity? quantity = null, Money? fee = null) =>
        Transaction.Imported(
            Owner,
            Account,
            TransactionType.Buy,
            AssetId,
            quantity ?? new Quantity(10m),
            Money.Euros(1m),
            Money.Euros(10m),
            fee ?? Money.Euros(0m),
            Occurrence.FromOffset(new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero), "Europe/Madrid"),
            TransactionSource.FromImport(ImportRun, "txid-1", rowNumber: null, "fingerprint"));
}
