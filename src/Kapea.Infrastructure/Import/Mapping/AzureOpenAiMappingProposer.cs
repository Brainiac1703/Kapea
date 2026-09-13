using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Import.Mapping;

/// <summary>
/// Pide a Azure OpenAI que deduzca cómo se lee un formato desconocido.
/// </summary>
/// <remarks>
/// Devuelve un mapeo, jamás una cifra. Los importes los calcula después el motor
/// determinista aplicando el perfil, así que recalcular un ejercicio ya presentado da
/// siempre lo mismo aunque el modelo haya cambiado.
///
/// Cualquier fallo —red, servicio caído, respuesta que no se entiende— se traduce en no
/// tener propuesta, no en romper la importación: el mapeo manual cubre su ausencia.
/// </remarks>
public sealed class AzureOpenAiMappingProposer(
    HttpClient http,
    IOptions<AzureOpenAiOptions> options,
    ILogger<AzureOpenAiMappingProposer> logger) : IMappingProposer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsAvailable => options.Value.IsConfigured;

    public async Task<MappingProposal?> ProposeAsync(
        PlatformCode platform,
        MappingSample sample,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!IsAvailable)
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
                    new { role = "user", content = Describe(platform, sample) },
                },
                temperature = 0,
                response_format = new { type = "json_object" },
            };

            var url = $"openai/deployments/{settings.Deployment}/chat/completions?api-version={settings.ApiVersion}";
            using var response = await http.PostAsJsonAsync(url, request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "El servicio de propuestas ha respondido {Codigo}. Se mapeará a mano.", (int)response.StatusCode);

                return null;
            }

            var completion = await response.Content
                .ReadFromJsonAsync<ChatCompletion>(Json, cancellationToken)
                .ConfigureAwait(false);

            var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("El servicio de propuestas ha respondido sin contenido.");

                return null;
            }

            return Parse(content);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Una propuesta es un atajo. Que falle no puede impedir importar, así que se
            // registra y se sigue por la vía manual.
            logger.LogWarning(exception, "No se ha podido obtener una propuesta de mapeo. Se mapeará a mano.");

            return null;
        }
    }

    /// <summary>
    /// Interpreta la propuesta.
    /// </summary>
    /// <remarks>
    /// Una respuesta que no encaja se descarta entera en lugar de aprovechar los trozos
    /// que se entiendan: un mapeo a medias es peor que ninguno, porque parece bueno.
    /// </remarks>
    internal static MappingProposal? Parse(string content)
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

        if (payload?.Columns is not { Count: > 0 } columns)
        {
            return null;
        }

        var fields = new List<FieldProposal>();

        foreach (var column in columns)
        {
            if (string.IsNullOrWhiteSpace(column.Column)
                || !Enum.TryParse<ImportField>(column.Field, ignoreCase: true, out var field))
            {
                continue;
            }

            fields.Add(new FieldProposal(field, column.Column.Trim(), Clamp(column.Confidence)));
        }

        if (fields.Count == 0)
        {
            return null;
        }

        var concepts = new List<ConceptProposal>();

        foreach (var concept in payload.Concepts ?? [])
        {
            if (!string.IsNullOrWhiteSpace(concept.Concept)
                && Enum.TryParse<TransactionType>(concept.Type, ignoreCase: true, out var type))
            {
                concepts.Add(new ConceptProposal(concept.Concept.Trim(), type, Clamp(concept.Confidence)));
            }
        }

        return new MappingProposal(
            fields,
            concepts,
            Enum.TryParse<DecimalConvention>(payload.DecimalConvention, ignoreCase: true, out var convention)
                ? convention
                : DecimalConvention.European,
            payload.DateFormats is { Count: > 0 } formats ? formats : ["dd/MM/yyyy"],
            Enum.TryParse<RowShape>(payload.RowShape, ignoreCase: true, out var shape) ? shape : RowShape.SingleMovement,
            Enum.TryParse<AmountSource>(payload.AmountSource, ignoreCase: true, out var source)
                ? source
                : AmountSource.Column,
            string.IsNullOrEmpty(payload.Delimiter) ? ';' : payload.Delimiter[0],
            string.IsNullOrWhiteSpace(payload.FixedCurrency) ? null : payload.FixedCurrency.Trim().ToUpperInvariant());
    }

    private static double Clamp(double confidence) => Math.Clamp(confidence, 0d, 1d);

    private static string Describe(PlatformCode platform, MappingSample sample)
    {
        var rows = sample.Rows.Select(row => string.Join(" | ", row));

        return $"""
            Plataforma: {platform}
            Cabeceras: {string.Join(" | ", sample.Headers)}
            Filas de ejemplo:
            {string.Join("\n", rows)}
            """;
    }

    private const string Instructions = """
        Eres un ayudante que deduce cómo leer un extracto de un bróker. Recibes las
        cabeceras de un fichero y como mucho tres filas de ejemplo, y devuelves un objeto
        JSON que describe qué columna corresponde a cada campo. No calcules importes ni
        interpretes operaciones: solo di qué columna es cuál.

        Devuelve exactamente esta forma:
        {
          "columns": [{"field": "...", "column": "...", "confidence": 0.0}],
          "concepts": [{"concept": "...", "type": "...", "confidence": 0.0}],
          "decimalConvention": "European|Invariant",
          "dateFormats": ["dd/MM/yyyy"],
          "rowShape": "SingleMovement|OpenPosition|OpenAndClosePosition",
          "amountSource": "Column|QuantityTimesPrice",
          "delimiter": ";",
          "fixedCurrency": "EUR"
        }

        Campos admitidos: Date, Concept, GrossAmount, AssetSymbol, Quantity, UnitPrice,
        Currency, Fee, Withholding, NaturalId, SplitRatio, OpenDate, OpenPrice, CloseDate,
        ClosePrice.

        Tipos admitidos: Buy, Sell, Deposit, Withdrawal, Transfer, Dividend, Fee,
        Interest, Reward, Split.

        Reglas:
        - Usa OpenAndClosePosition cuando una fila traiga a la vez apertura y cierre.
        - Usa QuantityTimesPrice cuando no haya columna con el importe total.
        - La confianza es tu seguridad real, de 0 a 1. Si dudas entre dos columnas
          parecidas, baja la confianza en lugar de elegir a ciegas.
        - No inventes columnas que no estén en las cabeceras.
        """;

    private sealed record ProposalPayload(
        [property: JsonPropertyName("columns")] IReadOnlyList<ColumnPayload>? Columns,
        [property: JsonPropertyName("concepts")] IReadOnlyList<ConceptPayload>? Concepts,
        [property: JsonPropertyName("decimalConvention")] string? DecimalConvention,
        [property: JsonPropertyName("dateFormats")] IReadOnlyList<string>? DateFormats,
        [property: JsonPropertyName("rowShape")] string? RowShape,
        [property: JsonPropertyName("amountSource")] string? AmountSource,
        [property: JsonPropertyName("delimiter")] string? Delimiter,
        [property: JsonPropertyName("fixedCurrency")] string? FixedCurrency);

    private sealed record ColumnPayload(string? Field, string? Column, double Confidence);

    private sealed record ConceptPayload(string? Concept, string? Type, double Confidence);

    private sealed record ChatCompletion(IReadOnlyList<Choice>? Choices);

    private sealed record Choice(Message? Message);

    private sealed record Message(string? Content);
}
