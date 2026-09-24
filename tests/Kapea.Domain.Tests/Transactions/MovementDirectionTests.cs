using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Transactions;

public class MovementDirectionTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());

    [Theory]
    [InlineData(TransactionType.Sell, MovementDirection.Gain)]
    [InlineData(TransactionType.Deposit, MovementDirection.Gain)]
    [InlineData(TransactionType.Dividend, MovementDirection.Gain)]
    [InlineData(TransactionType.Interest, MovementDirection.Gain)]
    [InlineData(TransactionType.Buy, MovementDirection.Loss)]
    [InlineData(TransactionType.Withdrawal, MovementDirection.Loss)]
    [InlineData(TransactionType.Fee, MovementDirection.Loss)]
    [InlineData(TransactionType.Transfer, MovementDirection.Neutral)]
    [InlineData(TransactionType.Split, MovementDirection.Neutral)]
    [InlineData(TransactionType.Unknown, MovementDirection.Neutral)]
    public void Each_kind_of_movement_says_whether_it_adds_or_takes_away(
        TransactionType type, MovementDirection expected) =>
        Assert.Equal(
            expected,
            // Una compra o una venta necesitan activo y cantidad; el resto no.
            MovementDirections.Of(Movement(type, quantity: type is TransactionType.Buy or TransactionType.Sell ? 1m : 0m)));

    [Fact]
    public void A_reward_paid_in_units_adds_even_though_no_euro_moved()
    {
        // Te la regalan: la cartera vale más que antes sin haber pagado nada. Mirar sólo
        // el efecto en caja la dejaba igual que una permuta, que no es lo mismo.
        var reward = Movement(TransactionType.Reward, quantity: 0.42m);

        Assert.Equal(MovementDirection.Gain, MovementDirections.Of(reward));
    }

    [Fact]
    public void A_swap_neither_adds_nor_takes_away()
    {
        // Cambias una cosa por otra: sus dos patas juntas no mueven la cartera.
        var leg = Movement(TransactionType.Buy, quantity: 1m, settledInCash: false);

        Assert.Equal(MovementDirection.Neutral, MovementDirections.Of(leg));
    }

    private static Transaction Movement(
        TransactionType type,
        decimal quantity = 0m,
        bool settledInCash = true) =>
        Transaction.Imported(
            Owner,
            Guid.NewGuid(),
            type,
            quantity > 0m ? Guid.NewGuid() : null,
            new Quantity(quantity),
            null,
            Money.Euros(100m),
            Money.Euros(0m),
            Occurrence.FromNaive(new DateTime(2026, 3, 1, 10, 0, 0), "Europe/Madrid"),
            TransactionSource.FromImport(Guid.NewGuid(), "txid", null, Guid.NewGuid().ToString()),
            settledInCash: settledInCash);
}
