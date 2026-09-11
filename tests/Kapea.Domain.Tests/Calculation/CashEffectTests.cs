using Kapea.Domain.Calculation;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class CashEffectTests
{
    [Fact]
    public void A_deposit_adds_its_amount() =>
        Assert.Equal(Money.Euros(1000m), CashEffect.Of(Cash(TransactionType.Deposit, 1000m)));

    [Fact]
    public void A_withdrawal_takes_its_amount_out() =>
        Assert.Equal(Money.Euros(-1000m), CashEffect.Of(Cash(TransactionType.Withdrawal, 1000m)));

    [Fact]
    public void A_purchase_takes_out_what_it_costs() =>
        Assert.Equal(Money.Euros(-500m), CashEffect.Of(Trade(TransactionType.Buy, 500m)));

    [Fact]
    public void A_sale_brings_in_what_it_yields() =>
        Assert.Equal(Money.Euros(500m), CashEffect.Of(Trade(TransactionType.Sell, 500m)));

    [Fact]
    public void The_fee_of_a_purchase_comes_out_of_the_balance_too()
    {
        // La comisión sale de la misma caja que el importe. Sumar solo el bruto deja un
        // saldo que nunca cuadra con el extracto, y por poco.
        Assert.Equal(Money.Euros(-509.95m), CashEffect.Of(Trade(TransactionType.Buy, 500m, fee: 9.95m)));
    }

    [Fact]
    public void A_dividend_in_cash_enters_net_of_the_withholding()
    {
        var dividend = Cash(TransactionType.Dividend, 100m, fee: 1m, withholding: 19m);

        Assert.Equal(Money.Euros(80m), CashEffect.Of(dividend));
    }

    [Theory]
    [InlineData(TransactionType.Reward)]
    [InlineData(TransactionType.Interest)]
    [InlineData(TransactionType.Dividend)]
    public void An_income_paid_in_units_does_not_pass_through_the_till(TransactionType type)
    {
        // Una recompensa de staking entrega monedas, no euros. Sumarla al efectivo
        // inventaría un dinero que nunca estuvo en la cuenta, y encima tributable.
        var reward = Trade(type, 12m, quantity: 340m);

        Assert.Equal(Money.Euros(0m), CashEffect.Of(reward));
    }

    [Fact]
    public void A_split_leaves_the_money_where_it_was() =>
        Assert.Equal(Money.Euros(0m), CashEffect.Of(Trade(TransactionType.Split, 0m, quantity: 10m)));

    [Fact]
    public void Moving_an_asset_between_accounts_only_costs_the_network_fee() =>
        Assert.Equal(Money.Euros(-0.35m), CashEffect.Of(Trade(TransactionType.Transfer, 0m, fee: 0.35m, quantity: 1m)));

    [Fact]
    public void A_movement_of_money_with_no_direction_is_not_guessed()
    {
        // Sin activo y sin signo, el movimiento no dice si el dinero entra o sale.
        // Suponerlo daría un saldo creíble y equivocado.
        Assert.Null(CashEffect.Of(Cash(TransactionType.Transfer, 200m)));
    }

    [Fact]
    public void An_unclassified_movement_has_no_effect_that_can_be_stated() =>
        Assert.Null(CashEffect.Of(Cash(TransactionType.Unknown, 200m)));

    [Fact]
    public void The_effect_keeps_the_currency_of_the_operation()
    {
        var inDollars = Cash(TransactionType.Deposit, 1000m, Currency.FromCode("USD"));

        Assert.Equal(Currency.FromCode("USD"), CashEffect.Of(inDollars)!.Value.Currency);
    }

    private static Transaction Cash(
        TransactionType type,
        decimal gross,
        Currency? currency = null,
        decimal fee = 0m,
        decimal? withholding = null) =>
        Build(type, assetId: null, Quantity.Zero, gross, currency ?? Currency.Euro, fee, withholding);

    private static Transaction Trade(
        TransactionType type,
        decimal gross,
        decimal fee = 0m,
        decimal quantity = 10m) =>
        Build(type, Guid.NewGuid(), new Quantity(quantity), gross, Currency.Euro, fee, withholding: null);

    private static Transaction Build(
        TransactionType type,
        Guid? assetId,
        Quantity quantity,
        decimal gross,
        Currency currency,
        decimal fee,
        decimal? withholding) =>
        Transaction.Imported(
            new UserId(Guid.NewGuid()),
            Guid.NewGuid(),
            type,
            assetId,
            quantity,
            null,
            new Money(gross, currency),
            new Money(fee, currency),
            Occurrence.FromNaive(new DateTime(2026, 1, 15, 10, 0, 0), "Europe/Madrid"),
            TransactionSource.FromImport(Guid.NewGuid(), "txid", null, "huella"),
            withholding is null ? null : new Money(withholding.Value, currency),
            currency.IsEuro
                ? null
                : ExchangeRate.Create(
                    currency,
                    1.10m,
                    new DateOnly(2026, 1, 15),
                    new DateOnly(2026, 1, 15),
                    ExchangeRate.EuropeanCentralBank));
}
