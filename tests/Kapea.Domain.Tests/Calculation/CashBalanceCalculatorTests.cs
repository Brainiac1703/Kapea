using Kapea.Domain.Calculation;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class CashBalanceCalculatorTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Currency Dollar = Currency.FromCode("USD");

    [Fact]
    public void The_balance_of_an_account_is_what_went_in_less_what_went_out()
    {
        var account = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Buy, 300m, fee: 5m, quantity: 10m),
            Valued(account, TransactionType.Withdrawal, 100m),
        ]);

        Assert.Equal(Money.Euros(595m), Assert.Single(balances.Balances).Amount);
        Assert.True(balances.IsComplete);
    }

    [Fact]
    public void An_account_that_has_operated_in_two_currencies_keeps_one_balance_for_each()
    {
        // Sumarlas daría un número sin significado: mil dólares no son mil euros, y una
        // cuenta puede tener saldo en varias divisas a la vez.
        var account = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Deposit, 500m, currency: Dollar),
        ]);

        Assert.Equal(2, balances.Balances.Count);
        Assert.Equal(Money.Euros(1000m), balances.Balances.Single(b => b.Currency.IsEuro).Amount);
        Assert.Equal(new Money(500m, Dollar), balances.Balances.Single(b => b.Currency == Dollar).Amount);
    }

    [Fact]
    public void Each_account_carries_its_own_balance()
    {
        var xtb = Guid.NewGuid();
        var kraken = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(xtb, TransactionType.Deposit, 1000m),
            Valued(kraken, TransactionType.Deposit, 250m),
        ]);

        Assert.Equal(2, balances.Balances.Count);
    }

    [Fact]
    public void An_unclassified_movement_stays_out_and_leaves_the_balance_incomplete()
    {
        // Si resulta ser una venta mueve el saldo y si resulta ser un traspaso no, así
        // que la única cifra honesta es la de sin él, diciendo que falta.
        var account = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Unknown, 400m),
        ]);

        Assert.Equal(Money.Euros(1000m), Assert.Single(balances.Balances).Amount);
        Assert.Equal(1, balances.UnresolvedMovements);
        Assert.False(balances.IsComplete);
    }

    [Fact]
    public void A_transfer_still_pending_review_stays_out_too()
    {
        var account = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(account, TransactionType.Sell, 400m, quantity: 5m, pendingTransfer: true),
        ]);

        Assert.Empty(balances.Balances);
        Assert.Equal(1, balances.UnresolvedMovements);
    }

    [Fact]
    public void A_confirmed_internal_transfer_leaves_the_money_where_it_was()
    {
        // Mover monedas de una cuenta propia a otra no gasta ni ingresa dinero, y la
        // comisión de red se paga en el propio activo, no en euros.
        var origin = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var transfer = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(origin, TransactionType.Transfer, 0m, fee: 0.35m, quantity: 2m,
                transferId: transfer, transferDestination: destination),
            Valued(destination, TransactionType.Transfer, 0m, quantity: 2m, transferId: transfer),
        ]);

        Assert.Equal(Money.Euros(0m), balances.Balances.Single(b => b.AccountId == origin).Amount);
        Assert.Equal(Money.Euros(0m), balances.Balances.Single(b => b.AccountId == destination).Amount);
        Assert.True(balances.IsComplete);
    }

    [Fact]
    public void The_total_in_euros_converts_each_currency_at_the_rate_of_the_day()
    {
        var account = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Deposit, 110m, currency: Dollar),
        ]);

        var total = CashBalanceCalculator.InEuros(balances, new Dictionary<Currency, ExchangeRate> { [Dollar] = Rate() });

        Assert.Equal(Money.Euros(1100m), total.Total);
        Assert.True(total.IsComplete);
    }

    [Fact]
    public void A_currency_with_no_rate_is_named_and_left_out_of_the_total()
    {
        // Contarla como cero escondería dinero, y convertirla a ojo inventaría una cifra
        // que nadie puede comprobar.
        var account = Guid.NewGuid();

        var balances = CashBalanceCalculator.Calculate([
            Valued(account, TransactionType.Deposit, 1000m),
            Valued(account, TransactionType.Deposit, 500m, currency: Dollar),
        ]);

        var total = CashBalanceCalculator.InEuros(balances, new Dictionary<Currency, ExchangeRate>());

        Assert.Equal(Money.Euros(1000m), total.Total);
        Assert.Equal([Dollar], total.CurrenciesWithoutRate);
        Assert.False(total.IsComplete);
    }

    private static ExchangeRate Rate() =>
        ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), ExchangeRate.EuropeanCentralBank);

    private static ValuedTransaction Valued(
        Guid accountId,
        TransactionType type,
        decimal gross,
        decimal fee = 0m,
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
            new Money(fee, money),
            Occurrence.FromNaive(new DateTime(2026, 1, 15, 10, 0, 0), "Europe/Madrid"),
            TransactionSource.FromImport(Guid.NewGuid(), "txid", null, $"huella-{Guid.NewGuid()}"),
            appliedExchangeRate: money.IsEuro ? null : Rate());

        return ValuedTransaction.From(
            transaction,
            internalTransferId: transferId,
            transferDestinationAccountId: transferDestination,
            isPendingTransferReview: pendingTransfer);
    }
}
