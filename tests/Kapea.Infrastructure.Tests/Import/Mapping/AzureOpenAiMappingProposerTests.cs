using System.Net;
using System.Text;
using System.Text.Json;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Import.Mapping;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Tests.Import.Mapping;

/// <summary>
/// Comprueba el adaptador contra respuestas grabadas.
/// </summary>
/// <remarks>
/// No se llama al servicio real: lo que hay que probar es cómo se interpreta lo que
/// devuelve y qué se le envía, y ninguna de las dos cosas necesita salir a la red.
/// </remarks>
public class AzureOpenAiMappingProposerTests
{
    private const string WellFormed = """
        {
          "columns": [
            {"field": "Date", "column": "Fecha", "confidence": 0.98},
            {"field": "Concept", "column": "Concepto", "confidence": 0.95},
            {"field": "GrossAmount", "column": "Importe", "confidence": 0.97}
          ],
          "concepts": [{"concept": "Dividendo", "type": "Dividend", "confidence": 0.9}],
          "decimalConvention": "European",
          "dateFormats": ["dd/MM/yyyy"],
          "rowShape": "SingleMovement",
          "amountSource": "Column",
          "delimiter": ";",
          "fixedCurrency": "EUR"
        }
        """;

    [Fact]
    public async Task A_well_formed_proposal_is_understood()
    {
        var proposal = await ProposeAsync(Completion(WellFormed));

        Assert.NotNull(proposal);
        Assert.Equal("Fecha", proposal!.ColumnFor(ImportField.Date));
        Assert.Equal("Importe", proposal.ColumnFor(ImportField.GrossAmount));
        Assert.Equal(0.98, proposal.ConfidenceFor(ImportField.Date), 3);
        Assert.Equal(DecimalConvention.European, proposal.DecimalConvention);
        Assert.Equal(TransactionType.Dividend, Assert.Single(proposal.Concepts).Type);
        Assert.Equal("EUR", proposal.FixedCurrency);
    }

    [Theory]
    [InlineData("{ esto no es json")]
    [InlineData("{}")]
    [InlineData("{\"columns\": []}")]
    [InlineData("{\"columns\": [{\"field\": \"Inventado\", \"column\": \"X\", \"confidence\": 1}]}")]
    public async Task A_malformed_proposal_is_refused_without_breaking_the_import(string content)
    {
        // Aprovechar los trozos que se entiendan sería peor que descartarla: un mapeo a
        // medias parece bueno y produce cifras equivocadas.
        Assert.Null(await ProposeAsync(Completion(content)));
    }

    [Fact]
    public async Task A_service_that_fails_leaves_the_manual_route_open()
    {
        Assert.Null(await ProposeAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
    }

    [Fact]
    public async Task A_service_that_does_not_answer_leaves_the_manual_route_open()
    {
        var proposer = Proposer(new ThrowingHandler(new HttpRequestException("la red falló")));

        Assert.Null(await proposer.ProposeAsync(PlatformCode.Xtb, Sample()));
    }

    [Fact]
    public async Task The_request_carries_only_headers_and_at_most_three_rows()
    {
        // Un extracto es un dato personal. Este test mira la petición emitida y falla si
        // se cuela una cuarta fila, que es la única forma de que la garantía no se
        // deshaga en silencio con un cambio futuro.
        var handler = new RecordingHandler(Completion(WellFormed));
        var proposer = Proposer(handler);

        var sample = new MappingSample(
            ["Fecha", "Concepto", "Importe"],
            [
                ["01/03/2026", "Dividendo", "45,20"],
                ["02/03/2026", "Dividendo", "1,15"],
                ["03/03/2026", "Dividendo", "9,99"],
                ["04/03/2026", "SECRETO", "1.000.000,00"],
            ]);

        await proposer.ProposeAsync(PlatformCode.Xtb, sample);

        Assert.DoesNotContain("SECRETO", handler.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("1.000.000,00", handler.Body, StringComparison.Ordinal);
        Assert.Contains("01/03/2026", handler.Body, StringComparison.Ordinal);
        Assert.Contains("03/03/2026", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_an_endpoint_configured_the_proposer_says_it_is_not_available()
    {
        var proposer = new AzureOpenAiMappingProposer(
            new HttpClient(new RecordingHandler(Completion(WellFormed))) { BaseAddress = new Uri("https://ejemplo/") },
            Options.Create(new AzureOpenAiOptions()),
            NullLogger<AzureOpenAiMappingProposer>.Instance);

        Assert.False(proposer.IsAvailable);
    }

    [Fact]
    public async Task The_one_registered_without_a_service_never_proposes_anything()
    {
        var proposer = new UnavailableMappingProposer();

        Assert.False(proposer.IsAvailable);
        Assert.Null(await proposer.ProposeAsync(PlatformCode.Xtb, Sample()));
    }

    private static async Task<MappingProposal?> ProposeAsync(HttpResponseMessage response) =>
        await Proposer(new RecordingHandler(response)).ProposeAsync(PlatformCode.Xtb, Sample());

    private static AzureOpenAiMappingProposer Proposer(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ejemplo.openai.azure.com/") },
            Options.Create(new AzureOpenAiOptions
            {
                Endpoint = "https://ejemplo.openai.azure.com",
                Deployment = "gpt",
                ApiKey = "clave-de-prueba",
            }),
            NullLogger<AzureOpenAiMappingProposer>.Instance);

    private static MappingSample Sample() =>
        new(["Fecha", "Concepto", "Importe"], [["01/03/2026", "Dividendo", "45,20"]]);

    private static HttpResponseMessage Completion(string content)
    {
        var body = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } },
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
