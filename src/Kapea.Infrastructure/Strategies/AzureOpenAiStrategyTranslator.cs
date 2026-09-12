using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kapea.Application.Strategies;
using Kapea.Infrastructure.Import.Mapping;
using Kapea.Shared.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Strategies;

/// <summary>
/// Pide a Azure OpenAI que traduzca a reglas un método descrito en palabras.
/// </summary>
/// <remarks>
/// Mismo trato que el mapeo de una importación: devuelve una propuesta que una persona
/// revisa, jamás una señal ni una cifra de mercado. Las señales las calcula después el
/// motor determinista, así que dos ejecuciones dan lo mismo aunque el modelo cambie.
///
/// No acepta imágenes a propósito. Un modelo leyendo niveles de una gráfica da una
/// respuesta distinta cada vez, no se puede auditar y no se puede simular, cuando los
/// números exactos están guardados.
/// </remarks>
public sealed class AzureOpenAiStrategyTranslator(
    HttpClient http,
    IOptions<AzureOpenAiOptions> options,
    ILogger<AzureOpenAiStrategyTranslator> logger) : IStrategyTranslator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsAvailable => options.Value.IsConfigured;

    public async Task<StrategyProposal?> TranslateAsync(
        string description,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var content = await AskAsync(Instructions, description, json: true, cancellationToken).ConfigureAwait(false);

        return content is null ? null : Parse(content);
    }

    public async Task<string?> ExplainAsync(
        StrategyRulesRequest rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (!IsAvailable)
        {
            return null;
        }

        return await AskAsync(
            "Explica en castellano llano, en dos o tres frases, qué hace el sistema cuyas reglas te dan. "
            + "No añadas recomendaciones ni opiniones sobre si es bueno.",
            JsonSerializer.Serialize(rules, Json),
            json: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Interpreta la propuesta.
    /// </summary>
    /// <remarks>
    /// Una respuesta que no encaja se descarta entera en lugar de aprovechar los trozos
    /// que se entiendan: unas reglas a medias son peores que ninguna, porque parecen
    /// buenas.
    /// </remarks>
    internal static StrategyProposal? Parse(string content)
    {
        ProposalPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<ProposalPayload>(content, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload?.Entry is null)
        {
            return null;
        }

        var rules = new StrategyRulesRequest(payload.Entry, payload.Exit, payload.Target, payload.StopLoss);

        return new StrategyProposal(
            string.IsNullOrWhiteSpace(payload.Name) ? "Sistema propuesto" : payload.Name.Trim(),
            rules,
            payload.NotUnderstood ?? [],
            Math.Clamp(payload.Confidence ?? 0d, 0d, 1d));
    }

    private async Task<string?> AskAsync(
        string instructions,
        string message,
        bool json,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var messages = new object[]
        {
            new { role = "system", content = instructions },
            new { role = "user", content = message },
        };

        object request = json
            ? new { messages, temperature = 0, response_format = new { type = "json_object" } }
            : new { messages, temperature = 0 };

        try
        {
            var url = $"openai/deployments/{settings.Deployment}/chat/completions?api-version={settings.ApiVersion}";

            using var response = await http.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "El traductor de sistemas ha respondido {Codigo}. Las reglas se declararán a mano.",
                    (int)response.StatusCode);

                return null;
            }

            var completion = await response.Content
                .ReadFromJsonAsync<ChatCompletion>(Json, cancellationToken)
                .ConfigureAwait(false);

            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Una traducción es un atajo. Que falle no puede impedir declarar las reglas.
            logger.LogWarning(exception, "No se ha podido traducir el sistema. Se declarará a mano.");

            return null;
        }
    }

    /// <summary>
    /// Lo que el modelo tiene que devolver.
    /// </summary>
    /// <remarks>
    /// Se le da el vocabulario exacto porque las reglas se validan al guardarlas: un
    /// indicador que Kapea no sepa evaluar se rechaza, y más vale que el traductor lo
    /// sepa antes de inventárselo.
    /// </remarks>
    private static string Instructions =>
        """
        Traduces a reglas un método de especulación descrito en palabras. Devuelves JSON:
        {
          "name": "nombre corto del sistema",
          "entry": <condición>,
          "exit": <condición o null>,
          "target": {"kind": "...", "factor": 0.0} o null,
          "stopLoss": {"kind": "...", "factor": 0.0} o null,
          "notUnderstood": ["lo que no has sabido traducir, con las palabras del texto"],
          "confidence": 0.0
        }

        Una condición es:
        {"junction": "Comparison", "left": <término>, "comparison": "...", "right": <término>}
        o bien:
        {"junction": "All" | "Any", "children": [<condición>, ...]}

        Un término es {"operand": "...", "window": 50} o {"operand": "Constant", "value": 30}.

        Operandos válidos: Price, SimpleMovingAverage, ExponentialMovingAverage,
        RelativeStrengthIndex, MacdLine, MacdSignal, MacdDistance, BollingerUpper,
        BollingerMiddle, BollingerLower, AverageDailyRange, Constant.

        Comparaciones válidas: GreaterThan, LessThan, CrossesAbove, CrossesBelow.

        Clases de nivel válidas: Percentage, RangeMultiple, RiskMultiple. El factor de un
        porcentaje va en tanto por uno: un ocho por ciento es 0.08.

        Reglas del trabajo:
        - No inventes umbrales que el texto no diga. Lo que falte, ponlo en notUnderstood.
        - No propongas indicadores que no estén en la lista. Si el método usa otro, dilo
          en notUnderstood.
        - No des opiniones sobre el método ni recomiendes operar.
        - Un sistema necesita al menos una forma de cerrar la posición: exit, target o
          stopLoss.
        """;

    private sealed record ProposalPayload(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("entry")] ConditionResponse? Entry,
        [property: JsonPropertyName("exit")] ConditionResponse? Exit,
        [property: JsonPropertyName("target")] LevelResponse? Target,
        [property: JsonPropertyName("stopLoss")] LevelResponse? StopLoss,
        [property: JsonPropertyName("notUnderstood")] IReadOnlyList<string>? NotUnderstood,
        [property: JsonPropertyName("confidence")] double? Confidence);

    private sealed record ChatCompletion(IReadOnlyList<Choice>? Choices);

    private sealed record Choice(Message? Message);

    private sealed record Message(string? Content);
}
