using System.Globalization;
using Kapea.Domain.ImportProfiles;

namespace Kapea.Application.Import;

/// <summary>Por qué una propuesta no se puede aceptar sin preguntar.</summary>
public enum MappingDoubt
{
    /// <summary>Falta un campo sin el que no se puede interpretar ninguna fila.</summary>
    RequiredFieldMissing = 1,

    /// <summary>Quien lo propone no está lo bastante seguro.</summary>
    LowConfidence = 2,

    /// <summary>La columna propuesta no se puede leer así en las filas de ejemplo.</summary>
    SampleDisagrees = 3,

    /// <summary>Hay conceptos en el fichero que la propuesta no traduce.</summary>
    UntranslatedConcept = 4,
}

/// <param name="Doubts">Vacío si la propuesta se puede aplicar sin preguntar.</param>
public sealed record MappingReviewResult(IReadOnlyList<MappingDoubtDetail> Doubts)
{
    public bool IsConclusive => Doubts.Count == 0;
}

public sealed record MappingDoubtDetail(MappingDoubt Doubt, string Explanation);

/// <summary>
/// Decide si una propuesta de mapeo se puede aplicar sin preguntar.
/// </summary>
/// <remarks>
/// La confianza que declara el modelo no basta por sí sola: puede estar seguro y
/// equivocado. Por eso lo propuesto se contrasta con las filas de ejemplo, que es un
/// hecho verificable y no una opinión: si la columna que dice ser la fecha no se lee
/// como fecha en ninguna de las tres, la propuesta no vale por mucha seguridad que
/// declare.
/// </remarks>
public static class MappingReview
{
    public static MappingReviewResult Review(
        MappingProposal proposal,
        MappingSample sample,
        double confidenceThreshold)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(sample);

        var doubts = new List<MappingDoubtDetail>();

        foreach (var required in Required(proposal))
        {
            if (proposal.ColumnFor(required) is not { Length: > 0 })
            {
                doubts.Add(new MappingDoubtDetail(
                    MappingDoubt.RequiredFieldMissing,
                    $"No dice qué columna es {Describe(required)}."));
            }
            else if (proposal.ConfidenceFor(required) < confidenceThreshold)
            {
                doubts.Add(new MappingDoubtDetail(
                    MappingDoubt.LowConfidence,
                    $"No hay seguridad suficiente sobre qué columna es {Describe(required)}."));
            }
        }

        doubts.AddRange(Contradictions(proposal, sample));
        doubts.AddRange(Untranslated(proposal, sample));

        return new MappingReviewResult(doubts);
    }

    /// <summary>Contrasta lo propuesto con lo que dicen las filas de ejemplo.</summary>
    private static IEnumerable<MappingDoubtDetail> Contradictions(MappingProposal proposal, MappingSample sample)
    {
        var numbers = proposal.DecimalConvention == DecimalConvention.European
            ? CultureInfo.GetCultureInfo("es-ES")
            : CultureInfo.InvariantCulture;

        foreach (var field in new[] { ImportField.Date, ImportField.OpenDate, ImportField.CloseDate })
        {
            if (Values(proposal, sample, field) is not { Count: > 0 } dates)
            {
                continue;
            }

            if (!dates.Any(value => proposal.DateFormats.Any(format => DateTime.TryParseExact(
                    value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))))
            {
                yield return new MappingDoubtDetail(
                    MappingDoubt.SampleDisagrees,
                    $"La columna '{proposal.ColumnFor(field)}' no se lee como fecha en ninguna de las filas de ejemplo.");
            }
        }

        foreach (var field in new[]
                 {
                     ImportField.GrossAmount, ImportField.Quantity, ImportField.UnitPrice,
                     ImportField.OpenPrice, ImportField.ClosePrice,
                 })
        {
            if (Values(proposal, sample, field) is not { Count: > 0 } amounts)
            {
                continue;
            }

            if (!amounts.Any(value => decimal.TryParse(
                    value, NumberStyles.Number | NumberStyles.AllowLeadingSign, numbers, out _)))
            {
                yield return new MappingDoubtDetail(
                    MappingDoubt.SampleDisagrees,
                    $"La columna '{proposal.ColumnFor(field)}' no se lee como número en ninguna de las filas de ejemplo.");
            }
        }
    }

    /// <summary>
    /// Conceptos que aparecen en las filas y que la propuesta no traduce.
    /// </summary>
    /// <remarks>
    /// No bloquean la importación —entrarían sin clasificar— pero sí que se acepte sin
    /// mirar: un concepto sin traducir en tres filas suele significar que hay muchos más
    /// en el fichero entero.
    /// </remarks>
    private static IEnumerable<MappingDoubtDetail> Untranslated(MappingProposal proposal, MappingSample sample)
    {
        var translated = proposal.Concepts
            .Select(concept => concept.Concept)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = Values(proposal, sample, ImportField.Concept)
            .Where(value => !translated.Contains(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count > 0)
        {
            yield return new MappingDoubtDetail(
                MappingDoubt.UntranslatedConcept,
                $"No traduce estos conceptos del fichero: {string.Join(", ", missing)}.");
        }
    }

    private static List<string> Values(MappingProposal proposal, MappingSample sample, ImportField field)
    {
        if (proposal.ColumnFor(field) is not { Length: > 0 } column)
        {
            return [];
        }

        var index = -1;

        for (var position = 0; position < sample.Headers.Count; position++)
        {
            if (string.Equals(sample.Headers[position]?.Trim(), column, StringComparison.OrdinalIgnoreCase))
            {
                index = position;

                break;
            }
        }

        return index < 0
            ? []
            : [.. sample.Rows
                .Select(row => index < row.Count ? row[index]?.Trim() : null)
                .Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value!)];
    }

    private static IEnumerable<ImportField> Required(MappingProposal proposal) => proposal.RowShape switch
    {
        RowShape.OpenPosition => [ImportField.OpenDate, ImportField.Quantity, ImportField.OpenPrice],
        RowShape.OpenAndClosePosition =>
        [
            ImportField.OpenDate, ImportField.Quantity, ImportField.OpenPrice,
            ImportField.CloseDate, ImportField.ClosePrice,
        ],
        _ => proposal.AmountSource == AmountSource.Column
            ? [ImportField.Date, ImportField.GrossAmount]
            : [ImportField.Date, ImportField.Quantity, ImportField.UnitPrice],
    };

    private static string Describe(ImportField field) => field switch
    {
        ImportField.Date => "la fecha",
        ImportField.GrossAmount => "el importe",
        ImportField.OpenDate => "la fecha de apertura",
        ImportField.OpenPrice => "el precio de apertura",
        ImportField.CloseDate => "la fecha de cierre",
        ImportField.ClosePrice => "el precio de cierre",
        ImportField.Quantity => "la cantidad",
        ImportField.UnitPrice => "el precio unitario",
        _ => field.ToString(),
    };
}
