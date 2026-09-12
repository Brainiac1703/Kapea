using System.Net;
using System.Net.Http.Json;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class IdeaEndpointsTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task A_source_can_be_registered_and_read_back()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var created = await client.PostAsJsonAsync(
            "/api/ideas/sources", new CreateIdeaSourceRequest("Un analista", "canal-de-ejemplo"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var sources = await client.GetFromJsonAsync<List<IdeaSourceResponse>>("/api/ideas/sources");

        var source = Assert.Single(sources!);

        Assert.Equal("Un analista", source.Name);

        // Sin forma oficial de consultarla, la pantalla tiene que poder decirlo.
        Assert.False(source.CanBeWatched);
    }

    [Fact]
    public async Task An_idea_is_stored_with_its_levels_and_starts_open()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var source = await Source(client);

        var created = await (await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
                source.Id, "BTC", "Buy", new DateOnly(2026, 3, 1), 100m, 120m, 90m,
                "https://ejemplo/publicacion", "Lo dijo en el vídeo del lunes.")))
            .Content.ReadFromJsonAsync<IdeaResponse>();

        Assert.Equal("Open", created!.Outcome);
        Assert.Equal(120m, created.TargetInEuros);
        Assert.Equal("Un analista", created.SourceName);
    }

    [Fact]
    public async Task An_idea_with_its_levels_the_wrong_way_round_is_rejected()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var source = await Source(client);

        var response = await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
            source.Id, "BTC", "Buy", new DateOnly(2026, 3, 1), 100m, 90m, 120m, null, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_idea_of_an_unknown_symbol_is_kept_without_an_asset()
    {
        // La fuente puede hablar de un valor que no está en el catálogo: la idea vale
        // igual, solo que no se puede seguir contra una serie de precios.
        var client = factory.CreateClientFor(Guid.NewGuid());
        var source = await Source(client);

        var created = await (await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
                source.Id, "NOEXISTE", "Buy", new DateOnly(2026, 3, 1), null, null, null, null, null)))
            .Content.ReadFromJsonAsync<IdeaResponse>();

        Assert.Null(created!.AssetId);
        Assert.Equal("NOEXISTE", created.Symbol);
    }

    [Fact]
    public async Task Without_an_extraction_service_the_screen_is_told_so()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var extraction = await (await client.PostAsJsonAsync(
                "/api/ideas/extract", new ExtractIdeasRequest("Lo que dijo el analista.")))
            .Content.ReadFromJsonAsync<IdeaExtractionResponse>();

        Assert.False(extraction!.Available);
        Assert.Empty(extraction.Ideas);
        Assert.NotNull(extraction.Note);
    }

    [Fact]
    public async Task An_idea_past_its_term_is_resolved_as_expired()
    {
        // El plazo corre aunque no haya precios: una idea de hace meses ya no está viva.
        var client = factory.CreateClientFor(Guid.NewGuid());
        var source = await Source(client);

        await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
            source.Id, "BTC", "Buy", new DateOnly(2026, 1, 1), 100m, 120m, 90m, null, null));

        await client.PostAsync("/api/ideas/track", null);

        var ideas = await client.GetFromJsonAsync<List<IdeaResponse>>("/api/ideas");

        Assert.Equal("Expired", Assert.Single(ideas!).Outcome);
    }

    [Fact]
    public async Task The_balance_of_a_source_without_resolved_ideas_says_so_instead_of_zero()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var source = await Source(client);

        await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
            source.Id, "BTC", "Buy", new DateOnly(2026, 3, 1), 100m, 120m, 90m, null, null));

        var balances = await client.GetFromJsonAsync<List<SourceBalanceResponse>>("/api/ideas/balance");

        var balance = Assert.Single(balances!);

        Assert.Null(balance.ReturnNetOfFees);
        Assert.Equal(1, balance.Open);
    }

    [Fact]
    public async Task Tracking_without_prices_resolves_nothing_and_does_not_fail()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var source = await Source(client);

        // Publicada hoy: una idea antigua caducaría por plazo y eso sí la resuelve.
        await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
            source.Id, "BTC", "Buy", DateOnly.FromDateTime(DateTime.UtcNow), 100m, 120m, 90m, null, null));

        var response = await client.PostAsync("/api/ideas/track", null);

        Assert.True(response.IsSuccessStatusCode);

        var ideas = await client.GetFromJsonAsync<List<IdeaResponse>>("/api/ideas");

        Assert.Equal("Open", Assert.Single(ideas!).Outcome);
    }

    private static async Task<IdeaSourceResponse> Source(HttpClient client) =>
        (await (await client.PostAsJsonAsync(
                "/api/ideas/sources", new CreateIdeaSourceRequest("Un analista", null)))
            .Content.ReadFromJsonAsync<IdeaSourceResponse>())!;
}
