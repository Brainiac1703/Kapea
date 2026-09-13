using System.Net;
using Azure.Core;
using Kapea.Infrastructure.Http;

namespace Kapea.Infrastructure.Tests.Http;

public class AzureOpenAiTokenHandlerTests
{
    [Fact]
    public async Task The_call_carries_a_token_from_the_identity()
    {
        var credential = new CountingCredential("token-de-prueba");
        var inner = new RecordedResponseHandler().RespondWithContent("{}");

        using var client = Client(credential, inner);
        await client.GetAsync("https://ejemplo.openai.azure.com/openai/deployments");

        var authorization = inner.Requests[0].Headers.Authorization;

        Assert.Equal("Bearer", authorization!.Scheme);
        Assert.Equal("token-de-prueba", authorization.Parameter);
    }

    [Fact]
    public async Task No_key_travels_in_the_request()
    {
        var inner = new RecordedResponseHandler().RespondWithContent("{}");

        using var client = Client(new CountingCredential("token"), inner);
        await client.GetAsync("https://ejemplo.openai.azure.com/openai/deployments");

        Assert.DoesNotContain("api-key", inner.Requests[0].Headers.Select(header => header.Key));
    }

    [Fact]
    public async Task A_token_that_is_still_valid_is_not_asked_for_again()
    {
        // Pedirlo en cada llamada añadiría una ida y vuelta al servicio de identidad por
        // cada petición al modelo.
        var credential = new CountingCredential("token", TimeSpan.FromHours(1));
        var inner = new RecordedResponseHandler()
            .RespondWithContent("{}")
            .RespondWithContent("{}");

        using var client = Client(credential, inner);
        await client.GetAsync("https://ejemplo.openai.azure.com/openai/deployments");
        await client.GetAsync("https://ejemplo.openai.azure.com/openai/deployments");

        Assert.Equal(1, credential.Calls);
    }

    [Fact]
    public async Task A_token_about_to_expire_is_renewed()
    {
        // Un token que caduca mientras la petición viaja se rechazaría en el servidor, y
        // el reintento cuesta más que renovarlo antes de salir.
        var credential = new CountingCredential("token", TimeSpan.FromSeconds(30));
        var inner = new RecordedResponseHandler()
            .RespondWithContent("{}")
            .RespondWithContent("{}");

        using var client = Client(credential, inner);
        await client.GetAsync("https://ejemplo.openai.azure.com/openai/deployments");
        await client.GetAsync("https://ejemplo.openai.azure.com/openai/deployments");

        Assert.Equal(2, credential.Calls);
    }

    private static HttpClient Client(TokenCredential credential, HttpMessageHandler inner) =>
        new(new AzureOpenAiTokenHandler(credential) { InnerHandler = inner });

    private sealed class CountingCredential(string token, TimeSpan? lifetime = null) : TokenCredential
    {
        internal int Calls { get; private set; }

        public override AccessToken GetToken(TokenRequestContext context, CancellationToken cancellationToken)
        {
            Calls++;

            return new AccessToken(token, DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1)));
        }

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext context,
            CancellationToken cancellationToken) =>
            new(GetToken(context, cancellationToken));
    }
}
