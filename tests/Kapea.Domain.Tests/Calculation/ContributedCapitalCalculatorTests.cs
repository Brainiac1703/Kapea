using Kapea.Domain.Calculation;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class ContributedCapitalCalculatorTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Currency Dollar = Currency.FromCode("USD");

    [Fact]
    public void What_was_contributed_is_what_went_in_less_what_came_out()
    {
        var account = Guid.NewGuid();

        var contributed = ContributedCapitalCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 4850m),
            Valued(account, TransactionType.Withdrawal, 614.96m),
            Valued(account, TransactionType.Buy, 1000m, quantity: 1m),
        ]);

        Assert.Equal(Money.Euros(4850m), contributed.DepositedInEuros);
        Assert.Equal(Money.Euros(614.96m), contributed.WithdrawnInEuros);
        Assert.Equal(Money.Euros(4235.04m), contributed.NetInEuros);
        Assert.False(contributed.MissesAssetsFromOutside);
    }

    [Fact]
    public void Moving_money_between_your_own_accounts_contributes_nothing()
    {
        // Ese dinero ya estaba dentro. Contarlo inflaría a la vez lo ingresado y lo
        // retirado: el neto no cambiaría, pero las dos cifras que se enseñan mentirían.
        var transfer = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();

        var contributed = ContributedCapitalCalculator.Calculate([
            Valued(source, TransactionType.Withdrawal, 500m, transferId: transfer, transferDestination: destination),
            Valued(destination, TransactionType.Deposit, 500m, transferId: transfer),
        ]);

        Assert.Equal(Money.Euros(0m), contributed.NetInEuros);
        Assert.Equal(Money.Euros(0m), contributed.DepositedInEuros);
        Assert.Equal(Money.Euros(0m), contributed.WithdrawnInEuros);
    }

    [Fact]
    public void A_contribution_in_another_currency_counts_at_the_frozen_rate()
    {
        var account = Guid.NewGuid();

        var contributed = ContributedCapitalCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1100m, currency: Dollar),
        ]);

        // Mil cien dólares al tipo congelado de 1,10 son mil euros.
        Assert.Equal(Money.Euros(1000m), contributed.DepositedInEuros);
    }

    [Fact]
    public void An_asset_arriving_from_outside_is_not_a_contribution_but_is_declared()
    {
        var account = Guid.NewGuid();

        var contributed = ContributedCapitalCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Deposit, 0m, quantity: 0.5m),
        ]);

        Assert.Equal(Money.Euros(1000m), contributed.DepositedInEuros);
        Assert.True(contributed.MissesAssetsFromOutside);
    }

    [Fact]
    public void A_movement_that_is_not_resolved_yet_does_not_count()
    {
        var account = Guid.NewGuid();

        var contributed = ContributedCapitalCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Deposit, 500m, pendingTransfer: true),
        ]);

        Assert.Equal(Money.Euros(1000m), contributed.NetInEuros);
    }

    [Fact]
    public void The_difference_with_wealth_comes_in_euros_and_as_a_share()
    {
        var contributed = new ContributedCapital(Money.Euros(4850m), Money.Euros(614.96m), false);

        var result = contributed.ResultAgainst(Money.Euros(3473m));

        Assert.Equal(Money.Euros(-762.04m), result);
        Assert.Equal(-0.1799m, Math.Round(contributed.ShareAgainst(Money.Euros(3473m))!.Value, 4));
    }

    [Fact]
    public void Without_contributions_there_is_no_share_to_give()
    {
        // Dividir entre cero no es cero por ciento, y un cero ahí se leería como «ni
        // ganas ni pierdes».
        var contributed = ContributedCapital.None;

        Assert.False(contributed.HasContributions);
        Assert.Null(contributed.ShareAgainst(Money.Euros(100m)));
        Assert.Equal(Money.Euros(100m), contributed.ResultAgainst(Money.Euros(100m)));
    }

    private static ValuedTransaction Valued(
        Guid accountId,
        TransactionType type,
        decimal gross,
        decimal quantity = 0m,
        Currency? currency = null,
        bool pendingTransfer = false,
        Guid? transferId = null,
        Guid? transferDestination = null)
    {
        var money = currency ?? Currency.Euro;

        var transaction = Transaction.Imported(
            Owner,
            accountId,
            type,
            quantity > 0m ? Guid.NewGuid() : null,
            new Quantity(quantity),
            null,
            new Money(gross, money),
            new Money(0m, money),
            Occurrence.FromNaive(new DateTime(2026, 1, 15, 10, 0, 0), "Europe/Madrid"),
            TransactionSource.FromImport(Guid.NewGuid(), "txid", null, $"huella-{Guid.NewGuid()}"),
            appliedExchangeRate: money.IsEuro ? null : Rate());

        return ValuedTransaction.From(
            transaction,
            internalTransferId: transferId,
            transferDestinationAccountId: transferDestination,
            isPendingTransferReview: pendingTransfer);
    }

    private static ExchangeRate Rate() =>
        ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), ExchangeRate.EuropeanCentralBank);
}
