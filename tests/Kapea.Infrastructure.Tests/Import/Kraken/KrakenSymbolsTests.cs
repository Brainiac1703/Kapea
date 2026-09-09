using Kapea.Domain.Assets;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Import.Kraken;

namespace Kapea.Infrastructure.Tests.Import.Kraken;

public class KrakenSymbolsTests
{
    [Theory]
    [InlineData("XXBT", "BTC")]
    [InlineData("XBT", "BTC")]
    [InlineData("XETH", "ETH")]
    [InlineData("ZEUR", "EUR")]
    [InlineData("ZUSD", "USD")]
    [InlineData("SOL", "SOL")]
    public void A_historical_alias_resolves_to_the_canonical_symbol(string krakenAsset, string expected) =>
        Assert.Equal(expected, KrakenSymbols.ToCanonical(krakenAsset));

    [Theory]
    [InlineData("ETH.S", "ETH")]
    [InlineData("DOT.M", "DOT")]
    public void A_staking_variant_is_the_same_asset(string krakenAsset, string expected) =>
        Assert.Equal(expected, KrakenSymbols.ToCanonical(krakenAsset));

    [Theory]
    [InlineData("XXBTZEUR", "BTC", "EUR")]
    [InlineData("XETHZEUR", "ETH", "EUR")]
    [InlineData("XETHXXBT", "ETH", "BTC")]
    [InlineData("SOL/EUR", "SOL", "EUR")]
    public void A_trading_pair_splits_into_asset_and_counterparty(string pair, string expectedBase, string expectedQuote)
    {
        Assert.True(KrakenSymbols.TrySplitPair(pair, out var baseAsset, out var quoteAsset));
        Assert.Equal(expectedBase, baseAsset);
        Assert.Equal(expectedQuote, quoteAsset);
    }

    [Fact]
    public void An_unsplittable_pair_is_reported_as_such() =>
        Assert.False(KrakenSymbols.TrySplitPair("ZZZ", out _, out _));

    [Fact]
    public void A_fiat_counterparty_yields_a_currency() =>
        Assert.Equal(Currency.Euro, KrakenSymbols.QuoteCurrency("EUR"));

    [Fact]
    public void A_crypto_counterparty_yields_no_currency() =>
        Assert.Null(KrakenSymbols.QuoteCurrency("BTC"));

    [Fact]
    public void Crypto_assets_are_classified_as_crypto() =>
        Assert.Equal(AssetClass.Crypto, KrakenSymbols.ClassOf("XXBT"));
}
