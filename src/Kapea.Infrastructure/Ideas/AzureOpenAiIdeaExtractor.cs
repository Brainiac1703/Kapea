using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kapea.Application.Ideas;
using Kapea.Infrastructure.Import.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Ideas;

/// <summary>
/// Saca ideas de un texto con Azure OpenAI.
/// </summary>
/// <remarks>
/// El texto entra, las ideas salen y el texto no se guarda. Lo que el modelo no sepa
/// extraer se devuelve dicho, no completado: una idea con niveles inventados parece buena
/// y no lo es.
///
/// No decide nada. Las ideas se guardan solo cuando una persona las aprueba, y su
/// resultado lo mide después el seguimiento contra la serie de precios.
/// </remarks>
public sealed class AzureOpenAiIdeaExtractor(
    HttpClient http,
    IOptions<AzureOpenAiOptions> options,
    ILogger<AzureOpenAiIdeaExtractor> logger) : IIdeaExtractor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsAvailable => options.Value.IsConfigured;

    public async Task<IdeaExtraction?> ExtractAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var settings = options.Value;

        try
        {
            var request = new
            {
                messages = new object[]
                {
                    new { role = "system", content = Instructions },
                    new { role = "user", content = text },
                },
                temperature = 0,
                response_format = new { type = "json_object" },
            };

            var url = $"openai/deployments/{settings.Deployment}/chat/completions?api-version={settings.ApiVersion}";

            using var response = await http.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "El extractor de ideas ha respondido {Codigo}. Se anotarán a mano.", (int)response.StatusCode);

                return null;
            }

            var completion = await response.Content
                .ReadFromJsonAsync<ChatCompletion>(Json, cancellationToken)
                .ConfigureAwait(false);

            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            return string.IsNullOrWhiteSpace(content) ? null : Parse(content);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "No se han podido extraer ideas del texto. Se anotarán a mano.");

            return null;
        }
    }

    internal static IdeaExtraction? Parse(string content)
    {
        ExtractionPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<ExtractionPayload>(content, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null)
        {
            return null;
        }

        var ideas = (payload.Ideas ?? [])
            .Where(idea => !string.IsNullOrWhiteSpace(idea.Symbol))
            .Select(idea => new ExtractedIdea(
                idea.Symbol!.Trim().ToUpperInvariant(),
                string.Equals(idea.Direction, "Sell", StringComparison.OrdinalIgnoreCase) ? "Sell" : "Buy",
                idea.Entry,
                idea.Target,
                idea.StopLoss,
                string.IsNullOrWhiteSpace(idea.Note) ? null : idea.Note.Trim()))
            .ToList();

        return new IdeaExtraction(ideas, payload.NotUnderstood ?? []);
    }

    private static string Instructions =>
        """
        Sacas ideas de inversión concretas de un texto. Devuelves JSON:
        {
          "ideas": [
            {
              "symbol": "el valor tal como lo nombra el texto",
              "direction": "Buy" o "Sell",
              "entry": número o null,
              "target": número o null,
              "stopLoss": número o null,
              "note": "una línea con lo que dice el texto de ese valor"
            }
          ],
          "notUnderstood": ["lo que no has sabido extraer, con las palabras del texto"]
        }

        Reglas del trabajo:
        - Solo ideas sobre un valor concreto. Un comentario general del mercado no es una
          idea: no devuelvas ninguna y dilo en notUnderstood.
        - No inventes niveles. Si el texto no da precio, objetivo o salida, deja null.
        - No añadas valores que el texto no nombre.
        - No des tu opinión ni recomiendes nada.
        """;

    private sealed record ExtractionPayload(
        [property: JsonPropertyName("ideas")] IReadOnlyList<IdeaPayload>? Ideas,
        [property: JsonPropertyName("notUnderstood")] IReadOnlyList<string>? NotUnderstood);

    private sealed record IdeaPayload(
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("direction")] string? Direction,
        [property: JsonPropertyName("entry")] decimal? Entry,
        [property: JsonPropertyName("target")] decimal? Target,
        [property: JsonPropertyName("stopLoss")] decimal? StopLoss,
        [property: JsonPropertyName("note")] string? Note);

    private sealed record ChatCompletion(IReadOnlyList<Choice>? Choices);

    private sealed record Choice(Message? Message);

    private sealed record Message(string? Content);
}
