using System.Net;
using Kapea.Application.Abstractions;
using Kapea.Domain.Accounts;
using Kapea.Domain.Credentials;
using Kapea.Infrastructure.Import.Bit2Me;
using Kapea.Infrastructure.Import.Kraken;
using Kapea.Infrastructure.Secrets;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Secrets;

public class CredentialVerifierTests
{
    private static readonly ApiSecret KrakenSecret = new("clave", Convert.ToBase64String([1, 2, 3, 4]));

    [Fact]
    public async Task A_kraken_credential_the_platform_accepts_is_valid()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithContent("""{"error":[],"result":{"trades":{},"count":0}}""");

        var verification = await new KrakenCredentialVerifier(KrakenClient(handler)).VerifyAsync(KrakenSecret);

        Assert.True(verification.IsValid);
        Assert.Equal(CredentialScopes.Read, verification.Scopes);
    }

    [Fact]
    public async Task A_kraken_credential_the_platform_rejects_carries_the_reason()
    {
        var handler = new RecordedResponseHandler().RespondWithContent("""{"error":["EAPI:Invalid key"]}""");

        var verification = await new KrakenCredentialVerifier(KrakenClient(handler)).VerifyAsync(KrakenSecret);

        Assert.False(verification.IsValid);
        Assert.Contains("EAPI:Invalid key", verification.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_kraken_secret_that_is_not_base64_is_reported_as_such()
    {
        var handler = new RecordedResponseHandler();

        var verification = await new KrakenCredentialVerifier(KrakenClient(handler))
            .VerifyAsync(new ApiSecret("clave", "esto no es base64 !!"));

        Assert.False(verification.IsValid);
        Assert.Contains("base64", verification.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bit2me_credential_the_platform_accepts_is_valid()
    {
        var handler = new RecordedResponseHandler().RespondWithContent("""{"count":0,"data":[]}""");

        var verification = await new Bit2MeCredentialVerifier(Bit2MeClient(handler))
            .VerifyAsync(new ApiSecret("clave", "secreto"));

        Assert.True(verification.IsValid);
        Assert.Equal(CredentialScopes.Read, verification.Scopes);
    }

    [Fact]
    public async Task A_bit2me_credential_without_read_permission_is_rejected()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(HttpStatusCode.Forbidden);

        var verification = await new Bit2MeCredentialVerifier(Bit2MeClient(handler))
            .VerifyAsync(new ApiSecret("clave", "secreto"));

        Assert.False(verification.IsValid);
        Assert.Contains("lectura", verification.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_verifier_declares_the_platform_it_answers_for()
    {
        Assert.Equal(Platform.Kraken, new KrakenCredentialVerifier(KrakenClient(new RecordedResponseHandler())).Platform);
        Assert.Equal(Platform.Bit2Me, new Bit2MeCredentialVerifier(Bit2MeClient(new RecordedResponseHandler())).Platform);
    }

    private static KrakenApiClient KrakenClient(RecordedResponseHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.kraken.com/") },
            NullLogger<KrakenApiClient>.Instance)
        {
            BackoffDelay = _ => TimeSpan.Zero,
        };

    private static Bit2MeApiClient Bit2MeClient(RecordedResponseHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://gateway.bit2me.com/") },
            NullLogger<Bit2MeApiClient>.Instance)
        {
            BackoffDelay = _ => TimeSpan.Zero,
        };
}
