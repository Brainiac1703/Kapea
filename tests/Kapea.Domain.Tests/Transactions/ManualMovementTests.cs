using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Transactions;

public class ManualMovementTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_manual_movement_needs_no_note()
    {
        var movement = Manual();

        Assert.Equal(TransactionOrigin.Manual, movement.Origin);
        Assert.Null(movement.Note);
        Assert.Equal(Now, movement.RegisteredAt);
        Assert.Null(movement.RevisedAt);
    }

    [Fact]
    public void A_manual_movement_never_passes_for_an_imported_one()
    {
        var movement = Manual();

        Assert.Null(movement.Source.ImportRunId);
        Assert.StartsWith("entry:", movement.Source.Fingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blank_note_is_stored_as_no_note() =>
        Assert.Null(Manual(note: "   ").Note);

    [Fact]
    public void A_manual_movement_follows_the_same_rules_as_an_imported_one()
    {
        // Sin activo: el mismo rechazo que tendría un importado igual de incoherente.
        var manual = Assert.Throws<DomainException>(() => Transaction.FromManualEntry(
            Owner, Account, TransactionType.Buy, assetId: null, new Quantity(1m), Money.Euros(10m),
            Money.Euros(10m), Money.Euros(0m), On(3), Now));

        var imported = Assert.Throws<DomainException>(() => Transaction.Imported(
            Owner, Account, TransactionType.Buy, assetId: null, new Quantity(1m), Money.Euros(10m),
            Money.Euros(10m), Money.Euros(0m), On(3),
            TransactionSource.FromImport(Guid.NewGuid(), "x", null, "f")));

        Assert.Equal(imported.Message, manual.Message);
    }

    [Fact]
    public void A_manual_movement_has_to_say_what_it_is() =>
        Assert.Throws<DomainException>(() => Manual(type: TransactionType.Unknown));

    [Fact]
    public void A_manual_movement_in_dollars_needs_its_rate() =>
        Assert.Throws<DomainException>(() => Transaction.FromManualEntry(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(1m), Usd(10m), Usd(10m), Usd(0m),
            On(3), Now));

    [Fact]
    public void Revising_changes_the_data_and_records_when()
    {
        var movement = Manual();
        var later = Now.AddHours(2);

        movement.Revise(
            Account, TransactionType.Buy, AssetId, new Quantity(3m), Money.Euros(10m), Money.Euros(30m),
            Money.Euros(0.5m), On(4), "corregida la cantidad", appliedExchangeRate: null, later);

        Assert.Equal(new Quantity(3m), movement.Quantity);
        Assert.Equal(Money.Euros(30m), movement.GrossAmount);
        Assert.Equal("corregida la cantidad", movement.Note);
        Assert.Equal(later, movement.RevisedAt);
        Assert.Equal(Now, movement.RegisteredAt);
    }

    [Fact]
    public void An_imported_movement_is_not_revised()
    {
        var exception = Assert.Throws<DomainException>(() => Imported().Revise(
            Account, TransactionType.Buy, AssetId, new Quantity(3m), Money.Euros(10m), Money.Euros(30m),
            Money.Euros(0m), On(4), null, null, Now));

        Assert.Contains("se corrige o se anula", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_adjustment_is_not_revised()
    {
        var adjustment = Transaction.FromManualAdjustment(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(1m), Money.Euros(10m), Money.Euros(10m),
            Money.Euros(0m), On(3), Guid.NewGuid(), "la cantidad venía mal");

        Assert.Throws<DomainException>(() => adjustment.Revise(
            Account, TransactionType.Buy, AssetId, new Quantity(3m), Money.Euros(10m), Money.Euros(30m),
            Money.Euros(0m), On(4), null, null, Now));
    }

    [Fact]
    public void An_adjustment_still_needs_its_reason() =>
        Assert.Throws<DomainException>(() => Transaction.FromManualAdjustment(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(1m), Money.Euros(10m), Money.Euros(10m),
            Money.Euros(0m), On(3), Guid.NewGuid(), " "));

    [Fact]
    public void A_revision_that_breaks_the_rules_leaves_the_movement_as_it_was()
    {
        var movement = Manual();

        Assert.Throws<DomainException>(() => movement.Revise(
            Account, TransactionType.Buy, AssetId, Quantity.Zero, Money.Euros(10m), Money.Euros(30m),
            Money.Euros(0m), On(4), null, null, Now));

        Assert.Equal(new Quantity(2m), movement.Quantity);
        Assert.Null(movement.RevisedAt);
    }

    private static Transaction Manual(string? note = null, TransactionType type = TransactionType.Buy) =>
        Transaction.FromManualEntry(
            Owner, Account, type, AssetId, new Quantity(2m), Money.Euros(10m), Money.Euros(20m),
            Money.Euros(0.1m), On(3), Now, note);

    internal static Transaction Imported(TransactionType type = TransactionType.Buy, decimal quantity = 2m, int day = 3) =>
        Transaction.Imported(
            Owner, Account, type, AssetId, new Quantity(quantity), Money.Euros(10m), Money.Euros(quantity * 10m),
            Money.Euros(0.1m), On(day), TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString("N"), null,
                Guid.NewGuid().ToString("N")));

    internal static Occurrence On(int day) =>
        Occurrence.FromOffset(new DateTimeOffset(2026, 9, day, 12, 0, 0, TimeSpan.Zero), "Europe/Madrid");

    private static Money Usd(decimal amount) => new(amount, Currency.FromCode("USD"));
}

public class VoidingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Voiding_keeps_the_data_and_the_origin()
    {
        var imported = ManualMovementTests.Imported();
        var fingerprint = imported.Source.Fingerprint;

        imported.Void("venta duplicada", Now);

        Assert.True(imported.IsVoided);
        Assert.Equal("venta duplicada", imported.VoidReason);
        Assert.Equal(Now, imported.VoidedAt);
        Assert.Equal(fingerprint, imported.Source.Fingerprint);
        Assert.Equal(new Quantity(2m), imported.Quantity);
    }

    [Fact]
    public void Voiding_needs_a_reason() =>
        Assert.Throws<DomainException>(() => ManualMovementTests.Imported().Void("  ", Now));

    [Fact]
    public void A_manual_movement_is_deleted_rather_than_voided()
    {
        var manual = Transaction.FromManualEntry(
            new UserId(Guid.NewGuid()), Guid.NewGuid(), TransactionType.Deposit, null, Quantity.Zero, null,
            Money.Euros(100m), Money.Euros(0m), ManualMovementTests.On(3), Now);

        var exception = Assert.Throws<DomainException>(() => manual.Void("sobra", Now));

        Assert.Contains("se borra", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Voiding_twice_is_rejected()
    {
        var imported = ManualMovementTests.Imported();
        imported.Void("duplicada", Now);

        Assert.Throws<DomainException>(() => imported.Void("otra vez", Now));
    }

    [Fact]
    public void Restoring_brings_back_exactly_what_there_was()
    {
        var imported = ManualMovementTests.Imported();
        var before = (imported.Quantity, imported.GrossAmount, imported.Type, imported.OccurredAt);

        imported.Void("duplicada", Now);
        imported.Restore();

        Assert.False(imported.IsVoided);
        Assert.Null(imported.VoidReason);
        Assert.Equal(before, (imported.Quantity, imported.GrossAmount, imported.Type, imported.OccurredAt));
    }

    [Fact]
    public void Restoring_something_not_voided_is_rejected() =>
        Assert.Throws<DomainException>(() => ManualMovementTests.Imported().Restore());
}
