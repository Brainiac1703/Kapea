using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Shared.Contracts;

namespace Kapea.Application.Import;

/// <summary>
/// Traduce entre las reglas que viajan en el contrato y las del dominio.
/// </summary>
/// <remarks>
/// El contrato lleva texto —«European», «OpenAndClosePosition»— y no números, porque
/// esas reglas se leen en una pantalla y en un JSON guardado, y un número obligaría a
/// tener la tabla de equivalencias delante para entender nada.
/// </remarks>
public static class ImportProfileRules
{
    public static ImportProfileVersion ToVersion(
        ImportProfileRulesRequest rules,
        int number,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return ImportProfileVersion.Create(
            number,
            createdAt,
            Delimiter(rules.Delimiter),
            Parse<DecimalConvention>(rules.DecimalConvention, "convención decimal"),
            rules.TimeZoneId,
            rules.RecognizedHeaders ?? [],
            Columns(rules.Columns),
            rules.DateFormats ?? [],
            Concepts(rules.Concepts),
            rules.NonFinancialConcepts,
            rules.FixedCurrency,
            Parse<RowShape>(rules.RowShape, "forma de la fila"),
            Parse<AmountSource>(rules.AmountSource, "origen del importe"),
            rules.FixedAssetClass,
            rules.AmountIsAlwaysPositive);
    }

    public static ImportProfileResponse ToResponse(ImportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ImportProfileResponse(
            profile.Id,
            profile.Platform.Value,
            profile.Name,
            profile.BuiltIn,
            profile.Current.Number,
            [.. profile.Versions.OrderByDescending(version => version.Number).Select(ToResponse)]);
    }

    public static ImportProfileVersionResponse ToResponse(ImportProfileVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new ImportProfileVersionResponse(
            version.Number,
            version.CreatedAt,
            version.Delimiter.ToString(),
            version.DecimalConvention.ToString(),
            version.TimeZoneId,
            version.FixedCurrency,
            version.RowShape.ToString(),
            version.AmountSource.ToString(),
            version.FixedAssetClass,
            version.AmountIsAlwaysPositive,
            version.RecognizedHeaders,
            version.DateFormats,
            version.NonFinancialConcepts,
            version.Columns.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
            version.Concepts.ToDictionary(entry => entry.Key, entry => entry.Value.ToString()));
    }

    /// <summary>Los campos a los que se puede apuntar una columna, con su nombre en pantalla.</summary>
    public static IReadOnlyList<ImportFieldResponse> Fields() =>
    [
        new(nameof(ImportField.Date), "Fecha", true),
        new(nameof(ImportField.Concept), "Concepto", false),
        new(nameof(ImportField.GrossAmount), "Importe", true),
        new(nameof(ImportField.AssetSymbol), "Activo", false),
        new(nameof(ImportField.Quantity), "Cantidad", false),
        new(nameof(ImportField.UnitPrice), "Precio unitario", false),
        new(nameof(ImportField.Currency), "Divisa", false),
        new(nameof(ImportField.Fee), "Comisión", false),
        new(nameof(ImportField.Withholding), "Retención", false),
        new(nameof(ImportField.NaturalId), "Identificador del origen", false),
        new(nameof(ImportField.SplitRatio), "Proporción del split", false),
        new(nameof(ImportField.OpenDate), "Fecha de apertura", false),
        new(nameof(ImportField.OpenPrice), "Precio de apertura", false),
        new(nameof(ImportField.CloseDate), "Fecha de cierre", false),
        new(nameof(ImportField.ClosePrice), "Precio de cierre", false),
    ];

    private static char Delimiter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException("El perfil necesita saber qué separa las columnas del fichero.");
        }

        // Se admite escrito como «\t» porque el tabulador no se puede teclear en un
        // campo de texto sin saltar al siguiente control.
        return value is "\\t" or "tab" ? '\t' : value[0];
    }

    private static IReadOnlyDictionary<ImportField, string> Columns(IReadOnlyDictionary<string, string>? columns)
    {
        var mapped = new Dictionary<ImportField, string>();

        foreach (var (field, column) in columns ?? new Dictionary<string, string>())
        {
            mapped[Parse<ImportField>(field, "campo del movimiento")] = column;
        }

        return mapped;
    }

    private static IReadOnlyDictionary<string, TransactionType> Concepts(IReadOnlyDictionary<string, string>? concepts)
    {
        var mapped = new Dictionary<string, TransactionType>(StringComparer.OrdinalIgnoreCase);

        foreach (var (concept, type) in concepts ?? new Dictionary<string, string>())
        {
            mapped[concept] = Parse<TransactionType>(type, "tipo de movimiento");
        }

        return mapped;
    }

    private static T Parse<T>(string? value, string what)
        where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new DomainException(
                $"'{value}' no es una {what} conocida. Admitidas: {string.Join(", ", Enum.GetNames<T>())}.");
}
