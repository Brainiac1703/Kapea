using System.Globalization;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Models;

/// <summary>
/// Las reglas de un perfil mientras se editan.
/// </summary>
/// <remarks>
/// Las listas y el mapeo viajan como texto de una sola línea porque un formulario con
/// una fila por cabecera y otra por concepto sería más difícil de rellenar que de
/// escribir. Se traducen al enviar.
/// </remarks>
public sealed class ProfileRulesModel
{
    /// <summary>
    /// Campos del movimiento a los que se puede apuntar, tal y como los da el servidor.
    /// </summary>
    /// <remarks>
    /// Viajan dentro del modelo porque el diálogo de Fluent UI solo recibe su Content:
    /// cualquier otro parámetro llegaría sin asignar y la lista saldría vacía.
    /// </remarks>
    public IReadOnlyList<ImportFieldResponse> AvailableFields { get; init; } = [];

    public string Name { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string Delimiter { get; set; } = ";";

    public string DecimalConvention { get; set; } = "European";

    public string TimeZoneId { get; set; } = "Europe/Madrid";

    public string FixedCurrency { get; set; } = "EUR";

    public string RowShape { get; set; } = "SingleMovement";

    public string AmountSource { get; set; } = "Column";

    public string FixedAssetClass { get; set; } = string.Empty;

    public bool AmountIsAlwaysPositive { get; set; }

    public string RecognizedHeaders { get; set; } = string.Empty;

    public string DateFormats { get; set; } = "dd/MM/yyyy";

    public string NonFinancialConcepts { get; set; } = string.Empty;

    /// <summary>Campo del movimiento a nombre de columna. Se rellena desde la lista de campos.</summary>
    public Dictionary<string, string> Columns { get; init; } = [];

    public string Concepts { get; set; } = string.Empty;

    public bool IsComplete => !string.IsNullOrWhiteSpace(RecognizedHeaders);

    public static ProfileRulesModel From(
        ImportProfileResponse profile,
        ImportProfileVersionResponse version,
        IReadOnlyList<ImportFieldResponse> fields)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(version);

        return new ProfileRulesModel
        {
            AvailableFields = fields,
            Name = profile.Name,
            Platform = profile.Platform,
            Delimiter = version.Delimiter == "\t" ? "\\t" : version.Delimiter,
            DecimalConvention = version.DecimalConvention,
            TimeZoneId = version.TimeZoneId,
            FixedCurrency = version.FixedCurrency ?? string.Empty,
            RowShape = version.RowShape,
            AmountSource = version.AmountSource,
            FixedAssetClass = version.FixedAssetClass ?? string.Empty,
            AmountIsAlwaysPositive = version.AmountIsAlwaysPositive,
            RecognizedHeaders = string.Join("; ", version.RecognizedHeaders),
            DateFormats = string.Join("; ", version.DateFormats),
            NonFinancialConcepts = string.Join("; ", version.NonFinancialConcepts),
            Columns = new Dictionary<string, string>(version.Columns),
            Concepts = string.Join(
                "\n",
                version.Concepts.Select(entry => $"{entry.Key} = {entry.Value}")),
        };
    }

    public ImportProfileRulesRequest ToRequest() =>
        new(
            Delimiter,
            DecimalConvention,
            TimeZoneId,
            string.IsNullOrWhiteSpace(FixedCurrency) ? null : FixedCurrency.Trim().ToUpperInvariant(),
            RowShape,
            AmountSource,
            string.IsNullOrWhiteSpace(FixedAssetClass) ? null : FixedAssetClass.Trim(),
            AmountIsAlwaysPositive,
            Split(RecognizedHeaders),
            Split(DateFormats),
            Split(NonFinancialConcepts),
            Columns
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                .ToDictionary(entry => entry.Key, entry => entry.Value.Trim()),
            ParseConcepts());

    private static string[] Split(string value) =>
        [.. value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>
    /// Lee las traducciones escritas como «concepto = tipo».
    /// </summary>
    /// <remarks>
    /// Una línea sin signo igual se ignora en lugar de romper el formulario entero: casi
    /// siempre es una línea a medio escribir, y perder lo demás por eso sería peor.
    /// </remarks>
    private Dictionary<string, string> ParseConcepts()
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in Concepts.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var concept = line[..separator].Trim();
            var type = line[(separator + 1)..].Trim();

            if (concept.Length > 0 && type.Length > 0)
            {
                parsed[concept] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(type);
            }
        }

        return parsed;
    }
}
