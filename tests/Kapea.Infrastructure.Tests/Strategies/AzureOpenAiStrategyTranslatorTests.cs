using Kapea.Application.Strategies;
using Kapea.Infrastructure.Import.Mapping;
using Kapea.Infrastructure.Strategies;
using Kapea.Infrastructure.Tests.Http;
using Kapea.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Tests.Strategies;

public class AzureOpenAiStrategyTranslatorTests
{
    [Fact]
    public async Task A_described_method_comes_back_as_rules()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("translation.json"));

        var proposal = await Translator(handler).TranslateAsync("Compro cuando cruza la media de cincuenta.");

        Assert.NotNull(proposal);
        Assert.Equal("Cruce de medias", proposal.Name);
        Assert.Equal("All", proposal.Rules.Entry.Junction);
        Assert.Equal(2, proposal.Rules.Entry.Children!.Count);
        Assert.Equal(0.08m, proposal.Rules.StopLoss!.Factor);
    }

    [Fact]
    public async Task What_it_could_not_translate_is_said_instead_of_filled_in()
    {
        // Una regla inventada para rellenar un hueco parece buena y no lo es.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("translation.json"));

        var proposal = await Translator(handler).TranslateAsync("Con volumen creciente.");

        Assert.Contains("volumen", Assert.Single(proposal!.NotUnderstood), StringComparison.Ordinal);
        Assert.Equal(0.82, proposal.Confidence);
    }

    [Fact]
    public async Task A_text_with_no_concrete_rules_produces_no_proposal()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("translation-empty.json"));

        Assert.Null(await Translator(handler).TranslateAsync("Hoy el mercado está nervioso."));
    }

    [Fact]
    public async Task A_response_that_does_not_parse_is_discarded_whole()
    {
        var handler = new RecordedResponseHandler().RespondWithContent(
            """{"choices":[{"message":{"content":"esto no es json"}}]}""");

        Assert.Null(await Translator(handler).TranslateAsync("Lo que sea."));
    }

    [Fact]
    public async Task A_service_that_fails_leaves_the_rules_to_be_written_by_hand()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(System.Net.HttpStatusCode.TooManyRequests);

        Assert.Null(await Translator(handler).TranslateAsync("Compro cuando cruza la media."));
    }

    [Fact]
    public async Task Without_a_service_configured_everything_still_works_by_hand()
    {
        var translator = new UnavailableStrategyTranslator();

        Assert.False(translator.IsAvailable);
        Assert.Null(await translator.TranslateAsync("Compro cuando cruza la media."));
        Assert.Null(await translator.ExplainAsync(Rules()));
    }

    [Fact]
    public async Task The_explanation_comes_back_as_plain_text()
    {
        var handler = new RecordedResponseHandler().RespondWithContent(
            """{"choices":[{"message":{"content":"Compra cuando el precio cruza su media de cincuenta días."}}]}""");

        var explanation = await Translator(handler).ExplainAsync(Rules());

        Assert.Contains("media de cincuenta", explanation!, StringComparison.Ordinal);
    }

    private static StrategyRulesRequest Rules() =>
        new(
            new ConditionResponse(
                "Comparison",
                new TermResponse("Price", null, null),
                "CrossesAbove",
                new TermResponse("SimpleMovingAverage", 50, null),
                null),
            null,
            null,
            new LevelResponse("Percentage", 0.08m));

    private static string Recorded(string fileName) => Path.Combine("Strategies", "Recorded", fileName);

    private static AzureOpenAiStrategyTranslator Translator(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo/") },
            Options.Create(new AzureOpenAiOptions
            {
                Endpoint = "https://ejemplo",
                Deployment = "kapea",
                ApiKey = "clave",
            }),
            NullLogger<AzureOpenAiStrategyTranslator>.Instance);
}
