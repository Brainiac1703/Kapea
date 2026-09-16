using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Transactions;

public class ManualCoincidenceTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Same_account_type_asset_quantity_and_day_coincide() =>
        Assert.True(ManualCoincidence.Matches(Manual(), Imported()));

    [Fact]
    public void Another_quantity_does_not_coincide() =>
        Assert.False(ManualCoincidence.Matches(Manual(), Imported(quantity: 3m)));

    [Fact]
    public void Another_day_does_not_coincide() =>
        Assert.False(ManualCoincidence.Matches(Manual(), Imported(at: At(4, 12))));

    [Fact]
    public void Another_type_does_not_coincide() =>
        Assert.False(ManualCoincidence.Matches(Manual(), Imported(type: TransactionType.Sell)));

    [Fact]
    public void Another_account_does_not_coincide() =>
        Assert.False(ManualCoincidence.Matches(Manual(), Imported(account: Guid.NewGuid())));

    [Fact]
    public void The_day_is_the_one_of_each_movement_in_its_own_zone()
    {
        // 23:30 en Madrid del día 3 son las 21:30 UTC del día 3; el mismo día para quien
        // compró, aunque uno venga en hora local y el otro en UTC.
        var manual = Manual(at: Occurrence.FromOffset(new DateTimeOffset(2026, 9, 3, 23, 30, 0, TimeSpan.FromHours(2)), "Europe/Madrid"));
        var imported = Imported(at: Occurrence.FromOffset(new DateTimeOffset(2026, 9, 3, 21, 30, 0, TimeSpan.Zero), "UTC"));

        Assert.True(ManualCoincidence.Matches(manual, imported));
    }

    [Fact]
    public void A_pair_marked_as_distinct_stops_coinciding()
    {
        var manual = Manual();
        var imported = Imported();

        manual.MarkDistinctFrom(imported);

        Assert.False(ManualCoincidence.Matches(manual, imported));
    }

    [Fact]
    public void Another_identical_import_coincides_again_after_marking_one_as_distinct()
    {
        var manual = Manual();
        manual.MarkDistinctFrom(Imported());

        Assert.True(ManualCoincidence.Matches(manual, Imported()));
    }

    [Fact]
    public void A_voided_import_does_not_coincide()
    {
        var imported = Imported();
        imported.Void("duplicada", Now);

        Assert.False(ManualCoincidence.Matches(Manual(), imported));
    }

    [Fact]
    public void Two_imports_are_left_to_deduplication() =>
        Assert.False(ManualCoincidence.Matches(Imported(), Imported()));

    private static Transaction Manual(Occurrence? at = null) =>
        Transaction.FromManualEntry(
            Owner, Account, TransactionType.Buy, AssetId, new Quantity(2m), Money.Euros(10m), Money.Euros(20m),
            Money.Euros(0m), at ?? At(3, 12), Now);

    private static Transaction Imported(
        decimal quantity = 2m, Occurrence? at = null, TransactionType type = TransactionType.Buy, Guid? account = null) =>
        Transaction.Imported(
            Owner, account ?? Account, type, AssetId, new Quantity(quantity), Money.Euros(10m),
            Money.Euros(quantity * 10m), Money.Euros(0m), at ?? At(3, 12),
            TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString("N"), null, Guid.NewGuid().ToString("N")));

    private static Occurrence At(int day, int hour) =>
        Occurrence.FromOffset(new DateTimeOffset(2026, 9, day, hour, 0, 0, TimeSpan.Zero), "UTC");
}
