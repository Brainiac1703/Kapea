using System.Security.Cryptography;
using System.Text;
using Kapea.Infrastructure.Import.Bit2Me;

namespace Kapea.Infrastructure.Tests.Import.Bit2Me;

public class Bit2MeSignatureTests
{
    [Fact]
    public void The_message_without_body_joins_nonce_and_url() =>
        Assert.Equal(
            "1687155308:/v1/trading/wallet/balance",
            Bit2MeSignature.MessageToSign(1687155308, "/v1/trading/wallet/balance", body: null));

    [Fact]
    public void The_message_with_body_appends_it() =>
        Assert.Equal(
            """1687155308:/v1/trading/order:{"side":"sell"}""",
            Bit2MeSignature.MessageToSign(1687155308, "/v1/trading/order", """{"side":"sell"}"""));

    [Fact]
    public void The_signature_is_hmac_sha512_over_the_sha256_digest_in_base64()
    {
        // Se reproduce a mano el algoritmo del ejemplo de la documentación: SHA-256 del
        // mensaje y HMAC-SHA512 de ese resumen con el secreto, en base64.
        const string secret = "secreto";
        var message = Bit2MeSignature.MessageToSign(1687155308, "/v1/trading/wallet/balance", null);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(message));

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(digest));

        Assert.Equal(expected, Bit2MeSignature.Compute(1687155308, "/v1/trading/wallet/balance", null, secret));
    }

    [Fact]
    public void A_different_nonce_yields_a_different_signature() =>
        Assert.NotEqual(
            Bit2MeSignature.Compute(1, "/v1/trading/trade", null, "secreto"),
            Bit2MeSignature.Compute(2, "/v1/trading/trade", null, "secreto"));

    [Fact]
    public void The_query_string_takes_part_in_the_signature() =>
        Assert.NotEqual(
            Bit2MeSignature.Compute(1, "/v1/trading/trade", null, "secreto"),
            Bit2MeSignature.Compute(1, "/v1/trading/trade?limit=100", null, "secreto"));

    [Fact]
    public void An_empty_secret_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Bit2MeSignature.Compute(1, "/v1/x", null, "   "));
}
