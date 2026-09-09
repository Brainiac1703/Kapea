using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.ValueObjects;

public class QuantityTests
{
    [Fact]
    public void A_negative_quantity_is_rejected() =>
        Assert.Throws<DomainException>(() => new Quantity(-0.00000001m));

    [Fact]
    public void Subtracting_more_than_available_throws() =>
        Assert.Throws<DomainException>(() => new Quantity(1m) - new Quantity(1.00000001m));

    [Fact]
    public void Crypto_precision_survives_addition()
    {
        // Ocho decimales es la unidad mínima de bitcoin; perderlos aquí falsearía
        // el coste del lote y, con él, el resultado de la venta.
        var total = new Quantity(0.00000001m) + new Quantity(0.00000002m);

        Assert.Equal(0.00000003m, total.Value);
    }

    [Fact]
    public void Eighteen_decimals_survive_a_round_trip()
    {
        var quantity = new Quantity(1.234567890123456789m);

        Assert.Equal(1.234567890123456789m, quantity.Value);
    }

    [Fact]
    public void Multiplying_a_quantity_by_a_unit_price_yields_money_in_that_currency()
    {
        var amount = new Quantity(2.5m) * Money.Euros(4m);

        Assert.Equal(10m, amount.Amount);
        Assert.Equal(Currency.Euro, amount.Currency);
    }

    [Fact]
    public void Min_returns_the_smaller_quantity() =>
        Assert.Equal(new Quantity(1m), Quantity.Min(new Quantity(1m), new Quantity(2m)));
}
