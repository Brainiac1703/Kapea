using System.Net;
using System.Net.Http.Json;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class StrategyEndpointsTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task A_new_user_already_has_a_strategy_to_start_from()
    {
        // Sin ninguno, la pantalla no enseñaría cómo se escribe una regla.
        var client = factory.CreateClientFor(Guid.NewGuid());

        var strategies = await client.GetFromJsonAsync<List<StrategyResponse>>("/api/strategies");

        var built = Assert.Single(strategies!);

        Assert.Equal(1, built.CurrentVersion);
        Assert.NotNull(built.Versions[0].Entry.Description);
        Assert.Equal(200, built.Versions[0].RequiredDays);
    }

    [Fact]
    public async Task The_vocabulary_comes_from_the_server()
    {
        // Añadir un indicador es tocar un sitio, y ninguna pantalla debería necesitar una
        // versión nueva para verlo.
        var client = factory.CreateClientFor(Guid.NewGuid());

        var vocabulary = await client.GetFromJsonAsync<StrategyVocabularyResponse>("/api/strategies/vocabulary");

        Assert.Contains(vocabulary!.Operands, operand => operand.Operand == "RelativeStrengthIndex");
        Assert.Contains(vocabulary.Comparisons, comparison => comparison.Value == "CrossesAbove");
        Assert.Contains(vocabulary.Operands, operand => operand.Operand == "Price" && !operand.NeedsWindow);
    }

    [Fact]
    public async Task A_strategy_can_be_created_and_read_back()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var created = await client.PostAsJsonAsync("/api/strategies", new CreateStrategyRequest(
            "Sobreventa",
            "Compra cuando la fuerza relativa baja de treinta.",
            Rules()));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var strategies = await client.GetFromJsonAsync<List<StrategyResponse>>("/api/strategies");

        Assert.Contains(strategies!, strategy => strategy.Name == "Sobreventa");
    }

    [Fact]
    public async Task A_rule_with_an_indicator_kapea_cannot_evaluate_is_rejected()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var rules = new StrategyRulesRequest(
            new ConditionResponse(
                "Comparison",
                new TermResponse("IndicadorInventado", 14, null),
                "LessThan",
                new TermResponse("Constant", null, 30m),
                null),
            null,
            null,
            new LevelResponse("Percentage", 0.1m));

        var response = await client.PostAsJsonAsync(
            "/api/strategies", new CreateStrategyRequest("Inventado", null, rules));

        // Conflicto, que es como la API traduce una regla del dominio incumplida.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_strategy_that_never_closes_a_position_is_rejected()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var rules = new StrategyRulesRequest(
            new ConditionResponse(
                "Comparison",
                new TermResponse("Price", null, null),
                "GreaterThan",
                new TermResponse("Constant", null, 100m),
                null),
            null,
            null,
            null);

        var response = await client.PostAsJsonAsync(
            "/api/strategies", new CreateStrategyRequest("Sin salida", null, rules));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Correcting_a_strategy_keeps_the_previous_version()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var created = await (await client.PostAsJsonAsync("/api/strategies", new CreateStrategyRequest(
                "Con versiones", null, Rules())))
            .Content.ReadFromJsonAsync<StrategyResponse>();

        var revised = await (await client.PostAsJsonAsync(
                $"/api/strategies/{created!.Id}/versions",
                new ReviseStrategyRequest(null, null, Rules(window: 21))))
            .Content.ReadFromJsonAsync<StrategyResponse>();

        Assert.Equal(2, revised!.CurrentVersion);
        Assert.Equal(2, revised.Versions.Count);
        Assert.Equal(14, revised.Versions[0].Entry.Left!.Window);
    }

    [Fact]
    public async Task Correcting_a_strategy_that_does_not_exist_is_reported_as_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            $"/api/strategies/{Guid.NewGuid()}/versions", new ReviseStrategyRequest(null, null, Rules()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Running_the_engine_without_prices_emits_nothing_and_does_not_fail()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var run = await (await client.PostAsync("/api/strategies/run", null))
            .Content.ReadFromJsonAsync<SignalRunResponse>();

        Assert.Equal(0, run!.Emitted);
        Assert.Equal(0, run.New);
    }

    [Fact]
    public async Task Simulating_a_strategy_that_does_not_exist_is_reported_as_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsync(
            $"/api/strategies/{Guid.NewGuid()}/simulate?assetId={Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_signals_of_a_new_user_come_back_empty_rather_than_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var signals = await client.GetFromJsonAsync<List<SignalResponse>>("/api/strategies/signals");

        Assert.Empty(signals!);
    }

    private static StrategyRulesRequest Rules(int window = 14) =>
        new(
            new ConditionResponse(
                "Comparison",
                new TermResponse("RelativeStrengthIndex", window, null),
                "LessThan",
                new TermResponse("Constant", null, 30m),
                null),
            null,
            null,
            new LevelResponse("Percentage", 0.1m));
}
