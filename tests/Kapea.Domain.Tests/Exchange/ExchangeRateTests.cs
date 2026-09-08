using Kapea.Domain.Calculation;
using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Exchange;

public class ExchangeRateTests
{
    private static readonly Currency Dollar = Currency.FromCode("USD");
    private static readonly UserId Owner = new(Guid.NewGuid());

    [Fact]
    public void Converting_a_dollar_amount_divides_by_the_units_per_euro()
    {
        var rate = ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 10), ExchangeRate.EuropeanCentralBank);

        Assert.Equal(Money.Euros(2000m), rate.ToEuros(new Money(2200m, Dollar)));
    }

    [Fact]
    public void Converting_an_amount_in_another_currency_throws()
    {
        var rate = ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 10), ExchangeRate.EuropeanCentralBank);

        Assert.Throws<CurrencyMismatchException>(() => rate.ToEuros(new Money(100m, Currency.FromCode("GBP"))));
    }

    [Fact]
    public void A_rate_dated_after_the_operation_is_rejected() =>
        Assert.Throws<DomainException>(() => ExchangeRate.Create(
            Dollar, 1.10m, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 11), ExchangeRate.EuropeanCentralBank));

    [Fact]
    public void A_non_positive_rate_is_rejected() =>
        Assert.Throws<DomainException>(() => ExchangeRate.Create(
            Dollar, 0m, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 10), ExchangeRate.EuropeanCentralBank));

    [Fact]
    public void The_rate_of_the_same_day_is_used_when_it_exists()
    {
        var resolved = ExchangeRateResolution.Resolve(Dollar, new DateOnly(2025, 3, 10), Published());

        Assert.NotNull(resolved);
        Assert.Equal(1.10m, resolved!.UnitsPerEuro);
        Assert.False(resolved.WasSubstituted);
    }

    [Fact]
    public void A_weekend_falls_back_to_the_last_published_rate()
    {
        // El 15 de marzo de 2025 fue sábado: el BCE no publica y se aplica el viernes.
        var resolved = ExchangeRateResolution.Resolve(Dollar, new DateOnly(2025, 3, 15), Published());

        Assert.NotNull(resolved);
        Assert.Equal(new DateOnly(2025, 3, 14), resolved!.RateDate);
        Assert.Equal(1.12m, resolved.UnitsPerEuro);
        Assert.True(resolved.WasSubstituted);
    }

    [Fact]
    public void A_bank_holiday_falls_back_to_the_last_published_rate()
    {
        var resolved = ExchangeRateResolution.Resolve(Dollar, new DateOnly(2025, 4, 18), Published());

        Assert.NotNull(resolved);
        Assert.Equal(new DateOnly(2025, 4, 17), resolved!.RateDate);
        Assert.True(resolved.WasSubstituted);
    }

    [Fact]
    public void A_date_before_any_publication_resolves_to_nothing() =>
        Assert.Null(ExchangeRateResolution.Resolve(Dollar, new DateOnly(2020, 1, 1), Published()));

    [Fact]
    public void Rates_of_another_currency_are_ignored() =>
        Assert.Null(ExchangeRateResolution.Resolve(Currency.FromCode("JPY"), new DateOnly(2025, 3, 10), Published()));

    [Fact]
    public void A_transaction_in_a_foreign_currency_without_a_frozen_rate_is_rejected()
    {
        var exception = Assert.Throws<DomainException>(() => Buy(new Money(2200m, Dollar), rate: null));

        Assert.Contains("congelado", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_transaction_in_euros_must_not_carry_a_rate() =>
        Assert.Throws<DomainException>(() => Buy(
            Money.Euros(2000m),
            ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 1), ExchangeRate.EuropeanCentralBank)));

    [Fact]
    public void Recalculating_uses_the_frozen_rate_even_if_the_published_rates_change()
    {
        // Se importa a 1,10. Después la fuente publica otro valor para esa misma fecha.
        // El movimiento ya no la consulta: la cifra declarada sigue siendo la misma.
        var frozen = ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 1), ExchangeRate.EuropeanCentralBank);
        var transaction = Buy(new Money(2200m, Dollar), frozen);

        var revisedTable = new[] { new DailyRate(Dollar, new DateOnly(2024, 2, 1), 1.50m) };
        var wouldBeNow = ExchangeRateResolution.Resolve(Dollar, new DateOnly(2024, 2, 1), revisedTable);

        Assert.Equal(Money.Euros(2000m), ValuedTransaction.From(transaction).GrossAmountInEuros);
        Assert.NotEqual(wouldBeNow!.UnitsPerEuro, transaction.AppliedExchangeRate!.UnitsPerEuro);
    }

    [Fact]
    public void A_frozen_rate_travels_with_the_transaction_into_the_calculation()
    {
        var frozen = ExchangeRate.Create(Dollar, 1.10m, new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 1), ExchangeRate.EuropeanCentralBank);
        var transaction = Buy(new Money(2200m, Dollar), frozen, fee: new Money(11m, Dollar));

        var valued = ValuedTransaction.From(transaction);

        Assert.Equal(Money.Euros(2000m), valued.GrossAmountInEuros);
        Assert.Equal(Money.Euros(10m), valued.FeeInEuros);
    }

    private static IReadOnlyList<DailyRate> Published() =>
    [
        new(Dollar, new DateOnly(2025, 3, 10), 1.10m),
        new(Dollar, new DateOnly(2025, 3, 14), 1.12m),
        new(Dollar, new DateOnly(2025, 4, 17), 1.13m),
        new(Currency.FromCode("GBP"), new DateOnly(2025, 3, 10), 0.84m),
    ];

    private static Transaction Buy(Money gross, ExchangeRate? rate, Money? fee = null) =>
        Transaction.Imported(
            Owner,
            Guid.NewGuid(),
            TransactionType.Buy,
            Guid.NewGuid(),
            new Quantity(10m),
            null,
            gross,
            fee ?? new Money(0m, gross.Currency),
            Occurrence.FromNaive(new DateTime(2024, 2, 1, 12, 0, 0), "Europe/Madrid"),
            TransactionSource.FromImport(Guid.NewGuid(), "txid", null, "fp"),
            appliedExchangeRate: rate);
}
