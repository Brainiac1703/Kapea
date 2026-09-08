using Kapea.Domain.Assets;
using Kapea.Domain.Common;

namespace Kapea.Domain.Tests.Assets;

public class AssetTests
{
    [Fact]
    public void Canonical_symbol_is_normalised_to_upper_case() =>
        Assert.Equal("BTC", Asset.Create("btc", AssetClass.Crypto).CanonicalSymbol);

    [Fact]
    public void An_asset_without_symbol_is_rejected() =>
        Assert.Throws<DomainException>(() => Asset.Create("  ", AssetClass.Crypto));

    [Fact]
    public void An_equity_accepts_an_isin() =>
        Assert.Equal("ES0113900J37", Asset.Create("SAN", AssetClass.Equity, "Banco Santander", "es0113900j37").Isin);

    [Fact]
    public void A_crypto_asset_rejects_an_isin() =>
        Assert.Throws<DomainException>(() => Asset.Create("BTC", AssetClass.Crypto, isin: "ES0113900J37"));

    [Theory]
    [InlineData("ES0113900J3")]
    [InlineData("1S0113900J37")]
    public void A_malformed_isin_is_rejected(string isin) =>
        Assert.Throws<DomainException>(() => Asset.Create("SAN", AssetClass.Equity, isin: isin));

    [Fact]
    public void An_asset_created_from_an_unresolved_symbol_is_unverified()
    {
        var asset = Asset.CreateUnverified("WEIRD", AssetClass.Crypto);

        Assert.False(asset.IsVerified);
    }

    [Fact]
    public void Verifying_an_asset_completes_its_details()
    {
        var asset = Asset.CreateUnverified("SAN", AssetClass.Equity);

        asset.Verify("Banco Santander", "ES0113900J37");

        Assert.True(asset.IsVerified);
        Assert.Equal("Banco Santander", asset.DisplayName);
        Assert.Equal("ES0113900J37", asset.Isin);
    }
}
