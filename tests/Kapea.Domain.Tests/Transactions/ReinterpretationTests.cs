using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Transactions;

/// <summary>
/// Comprueba cuándo se puede volver a clasificar un movimiento.
/// </summary>
/// <remarks>
/// Existe por un caso real: Bit2Me empezó a mandar una forma que el adaptador no
/// entendía, y esos movimientos entraron sin clasificar. Cuando aprendió a leerla, hizo
/// falta poder aprovecharlo sobre lo ya importado.
/// </remarks>
public class ReinterpretationTests
{
    [Fact]
    public void An_unclassified_movement_can_be_reinterpreted()
    {
        var transaction = Unclassified();

        transaction.Reinterpret(TransactionType.Deposit);

        Assert.Equal(TransactionType.Deposit, transaction.Type);
    }

    [Fact]
    public void The_figures_are_not_touched_when_reinterpreting()
    {
        // Las cifras son las que trajo el origen. Lo que se corrige es qué significan.
        var transaction = Unclassified();
        var amount = transaction.GrossAmount;
        var occurred = transaction.OccurredAt;

        transaction.Reinterpret(TransactionType.Deposit);

        Assert.Equal(amount, transaction.GrossAmount);
        Assert.Equal(occurred, transaction.OccurredAt);
    }

    [Fact]
    public void A_movement_that_is_already_classified_is_never_reinterpreted()
    {
        // Puede haber entrado en un ejercicio ya presentado, y cambiarle el tipo
        // alteraría cifras que alguien dio por buenas.
        var transaction = Classified(TransactionType.Deposit);

        var exception = Assert.Throws<DomainException>(() => transaction.Reinterpret(TransactionType.Withdrawal));

        Assert.Contains("sin clasificar", exception.Message, StringComparison.Ordinal);
        Assert.Equal(TransactionType.Deposit, transaction.Type);
    }

    [Fact]
    public void Reinterpreting_as_unknown_leaves_it_as_it_was()
    {
        var transaction = Unclassified();

        transaction.Reinterpret(TransactionType.Unknown);

        Assert.Equal(TransactionType.Unknown, transaction.Type);
    }

    private static Transaction Unclassified() => Classified(TransactionType.Unknown);

    private static Transaction Classified(TransactionType type) =>
        Transaction.Imported(
            new UserId(Guid.NewGuid()),
            Guid.NewGuid(),
            type,
            assetId: null,
            new Quantity(0m),
            unitPrice: null,
            new Money(500m, Currency.Euro),
            Money.Zero(Currency.Euro),
            Occurrence.FromNaive(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Unspecified), "Europe/Madrid"),
            TransactionSource.FromImport(
                Guid.NewGuid(),
                naturalId: "w-1",
                rowNumber: null,
                fingerprint: Guid.NewGuid().ToString("N"),
                rawContent: "{}"));
}
