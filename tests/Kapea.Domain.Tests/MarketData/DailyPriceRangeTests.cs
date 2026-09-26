using Kapea.Domain.MarketData;

namespace Kapea.Domain.Tests.MarketData;

/// <summary>
/// El recorrido del día: apertura, máximo y mínimo junto al cierre.
/// </summary>
public class DailyPriceRangeTests
{
    private static readonly Guid Asset = Guid.NewGuid();

    [Fact]
    public void A_price_without_a_range_is_not_a_range_of_zero()
    {
        var price = Close(100m);

        Assert.False(price.HasRange);
        Assert.Null(price.HighInEuros);
    }

    [Fact]
    public void A_coherent_range_is_kept()
    {
        var price = Close(100m).WithRange(open: 95m, high: 105m, low: 94m);

        Assert.True(price.HasRange);
        Assert.Equal(95m, price.OpenInEuros);
        Assert.Equal(105m, price.HighInEuros);
        Assert.Equal(94m, price.LowInEuros);
    }

    [Theory]
    [InlineData(95, 90, 100)]
    [InlineData(110, 105, 94)]
    [InlineData(90, 105, 94)]
    [InlineData(95, 99, 94)]
    [InlineData(95, 105, 101)]
    [InlineData(95, 105, -1)]
    public void An_incoherent_range_is_discarded_and_the_close_survives(decimal open, decimal high, decimal low)
    {
        // Perder el día entero por un extremo mal traído sería peor que quedarse sin
        // dibujar la vela.
        var price = Close(100m).WithRange(open, high, low);

        Assert.False(price.HasRange);
        Assert.Equal(100m, price.PriceInEuros);
    }

    [Theory]
    [InlineData(null, 105.0, 94.0)]
    [InlineData(95.0, null, 94.0)]
    [InlineData(95.0, 105.0, null)]
    public void A_partial_range_is_no_range(double? open, double? high, double? low) =>
        Assert.False(Close(100m).WithRange(Amount(open), Amount(high), Amount(low)).HasRange);

    private static decimal? Amount(double? value) => value is { } number ? (decimal)number : null;

    [Fact]
    public void A_flat_day_is_a_valid_range() =>
        Assert.True(Close(100m).WithRange(100m, 100m, 100m).HasRange);

    private static DailyPrice Close(decimal price) =>
        new(Asset, new DateOnly(2026, 3, 10), price, "Prueba");
}
