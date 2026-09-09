using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.ValueObjects;

public class MoneyTests
{
    private static readonly Currency Dollar = Currency.FromCode("usd");

    [Fact]
    public void Adding_two_amounts_in_the_same_currency_keeps_the_currency()
    {
        var result = Money.Euros(10.25m) + Money.Euros(4.75m);

        Assert.Equal(15m, result.Amount);
        Assert.Equal(Currency.Euro, result.Currency);
    }

    [Fact]
    public void Adding_amounts_in_different_currencies_throws()
    {
        var exception = Assert.Throws<CurrencyMismatchException>(
            () => Money.Euros(10m) + new Money(10m, Dollar));

        Assert.Equal(Currency.Euro, exception.Left);
        Assert.Equal(Dollar, exception.Right);
    }

    [Fact]
    public void Subtracting_amounts_in_different_currencies_throws() =>
        Assert.Throws<CurrencyMismatchException>(() => Money.Euros(10m) - new Money(1m, Dollar));

    [Fact]
    public void Comparing_amounts_in_different_currencies_throws() =>
        Assert.Throws<CurrencyMismatchException>(() => Money.Euros(10m) > new Money(1m, Dollar));

    [Fact]
    public void An_amount_without_currency_is_rejected() =>
        Assert.Throws<Kapea.Domain.Common.DomainException>(() => new Money(1m, default));

    [Fact]
    public void Currency_code_is_normalised_to_upper_case() =>
        Assert.Equal("USD", Currency.FromCode("usd").Code);

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("E1R")]
    [InlineData("   ")]
    public void An_invalid_currency_code_is_rejected(string code) =>
        Assert.Throws<Kapea.Domain.Common.DomainException>(() => Currency.FromCode(code));

    [Fact]
    public void Repeated_addition_of_a_decimal_fraction_does_not_drift()
    {
        // El mismo bucle con double da 0,9999999999999999: es exactamente el error
        // que haría que el total de un ejercicio no cuadre con la suma de sus líneas.
        var total = Money.Euros(0m);

        for (var i = 0; i < 10; i++)
        {
            total += Money.Euros(0.1m);
        }

        Assert.Equal(1m, total.Amount);
    }

    [Fact]
    public void Dividing_by_zero_throws() =>
        Assert.Throws<Kapea.Domain.Common.DomainException>(() => Money.Euros(10m) / 0m);

    [Fact]
    public void Rounding_for_display_uses_banker_rounding_and_keeps_the_currency()
    {
        var rounded = Money.Euros(2.345m).RoundForDisplay();

        Assert.Equal(2.34m, rounded.Amount);
        Assert.Equal(Currency.Euro, rounded.Currency);
    }
}
