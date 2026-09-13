using Kapea.Application.Ideas;
using Kapea.Infrastructure.Ideas;
using Kapea.Infrastructure.Import.Mapping;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Tests.Ideas;

public class AzureOpenAiIdeaExtractorTests
{
    [Fact]
    public async Task Several_ideas_in_one_text_come_back_separately()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("extraction.json"));

        var extraction = await Extractor(handler).ExtractAsync("Lo que dijo el analista.");

        Assert.Equal(2, extraction!.Ideas.Count);
        Assert.Equal("SAN.ES", extraction.Ideas[0].Symbol);
        Assert.Equal("Buy", extraction.Ideas[0].Direction);
        Assert.Equal(5.2m, extraction.Ideas[0].Target);
        Assert.Equal("Sell", extraction.Ideas[1].Direction);
    }

    [Fact]
    public async Task An_idea_without_levels_keeps_them_empty_instead_of_invented()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("extraction.json"));

        var extraction = await Extractor(handler).ExtractAsync("Lo que dijo el analista.");

        Assert.Null(extraction!.Ideas[1].Target);
        Assert.Null(extraction.Ideas[1].StopLoss);
    }

    [Fact]
    public async Task What_it_could_not_extract_is_said()
    {
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("extraction.json"));

        var extraction = await Extractor(handler).ExtractAsync("Lo que dijo el analista.");

        Assert.Contains("soporte", Assert.Single(extraction!.NotUnderstood), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_text_with_only_general_commentary_produces_no_ideas()
    {
        // Un comentario del mercado no es una idea, y decirlo es más útil que inventarla.
        var handler = new RecordedResponseHandler().RespondWithFile(Recorded("extraction-none.json"));

        var extraction = await Extractor(handler).ExtractAsync("Hoy el mercado está nervioso.");

        Assert.Empty(extraction!.Ideas);
        Assert.NotEmpty(extraction.NotUnderstood);
    }

    [Fact]
    public async Task A_response_that_does_not_parse_is_discarded()
    {
        var handler = new RecordedResponseHandler().RespondWithContent(
            """{"choices":[{"message":{"content":"no es json"}}]}""");

        Assert.Null(await Extractor(handler).ExtractAsync("Lo que sea."));
    }

    [Fact]
    public async Task A_service_that_fails_leaves_the_ideas_to_be_written_by_hand()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(System.Net.HttpStatusCode.BadGateway);

        Assert.Null(await Extractor(handler).ExtractAsync("Lo que dijo el analista."));
    }

    [Fact]
    public async Task Without_a_service_configured_everything_still_works_by_hand()
    {
        var extractor = new UnavailableIdeaExtractor();

        Assert.False(extractor.IsAvailable);
        Assert.Null(await extractor.ExtractAsync("Lo que sea."));
    }

    [Fact]
    public async Task A_source_that_cannot_be_watched_reports_nothing_new()
    {
        var watcher = new UnavailableSourceWatcher();

        Assert.False(watcher.IsAvailable);
        Assert.Empty(await watcher.SinceAsync("canal", null));
    }

    private static string Recorded(string fileName) => Path.Combine("Ideas", "Recorded", fileName);

    private static AzureOpenAiIdeaExtractor Extractor(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo/") },
            Options.Create(new AzureOpenAiOptions
            {
                Endpoint = "https://ejemplo",
                Deployment = "kapea",
                ApiKey = "clave",
            }),
            NullLogger<AzureOpenAiIdeaExtractor>.Instance);
}
