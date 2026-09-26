using Kapea.Domain.Assets;

namespace Kapea.Domain.Tests.Assets;

/// <summary>
/// En qué mercado cotiza un activo, leído de su símbolo.
/// </summary>
public class MarketTests
{
    [Theory]
    [InlineData("NOW.US", "Equity:US")]
    [InlineData("MSTR.US", "Equity:US")]
    [InlineData("VVSM.DE", "Equity:DE")]
    [InlineData("CSPX.UK", "Equity:UK")]
    public void The_suffix_of_the_symbol_is_the_exchange(string symbol, string expected) =>
        Assert.Equal(expected, Market.Of(symbol, AssetClass.Equity));

    [Fact]
    public void Two_exchanges_of_the_same_class_are_different_markets()
    {
        // Es lo que permite que un festivo de Wall Street no afecte a Fráncfort.
        Assert.NotEqual(Market.Of("NOW.US", AssetClass.Equity), Market.Of("VVSM.DE", AssetClass.Equity));
    }

    [Fact]
    public void Without_a_suffix_the_market_is_the_class()
    {
        // Las criptomonedas no llevan sufijo y cotizan siempre: caen todas en el mismo.
        Assert.Equal(Market.Of("BTC", AssetClass.Crypto), Market.Of("ETH", AssetClass.Crypto));
    }

    [Fact]
    public void The_same_suffix_in_two_classes_is_not_the_same_market() =>
        Assert.NotEqual(Market.Of("X.US", AssetClass.Equity), Market.Of("X.US", AssetClass.Crypto));

    [Theory]
    [InlineData("NOW.")]
    [InlineData(".US")]
    public void A_symbol_with_nothing_on_one_side_of_the_dot_falls_back_to_the_class(string symbol) =>
        Assert.Equal("Equity", Market.Of(symbol, AssetClass.Equity));

    [Fact]
    public void The_suffix_is_read_regardless_of_case() =>
        Assert.Equal(Market.Of("NOW.US", AssetClass.Equity), Market.Of("now.us", AssetClass.Equity));
}
