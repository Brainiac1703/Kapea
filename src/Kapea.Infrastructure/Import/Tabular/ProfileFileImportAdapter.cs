using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>
/// Lee un fichero aplicando un perfil.
/// </summary>
/// <remarks>
/// Es el sustituto de escribir un adaptador por plataforma. Lo que antes era código
/// —qué columna es la fecha, cómo vienen los números, qué significa «Stocks/ETF sale»—
/// aquí son datos que llegan en la versión del perfil.
///
/// No adivina nada. Una columna que el perfil no mapea no se usa, y un concepto sin
/// traducción entra como desconocido en lugar de suponerle un tipo: un movimiento mal
/// clasificado es una cifra mal calculada, y esto acaba en una declaración.
/// </remarks>
public sealed class ProfileFileImportAdapter
{
    public ImportReadResult Read(
        ImportProfile profile,
        ImportProfileVersion version,
        TabularContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(content);

        var columns = ResolveColumns(version, content.Headers);
        var values = new ProfileValueReader(version);
        var records = new List<ImportRecord>();
        var rejected = new List<RejectedRecord>();
        var warnings = new List<string>();
        var nonFinancial = 0;
        var unknownConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (field, header) in version.Columns)
        {
            if (!columns.ContainsKey(field))
            {
                warnings.Add($"El fichero no trae la columna '{header}': los movimientos irán sin ese dato.");
            }
        }

        foreach (var row in content.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var concept = Cell(row, columns, ImportField.Concept);

            // Los apuntes sin efecto financiero se cuentan y se descartan. Rechazarlos
            // los mostraría como un problema a resolver cuando no hay nada que resolver.
            if (concept is { Length: > 0 } text
                && version.NonFinancialConcepts.Any(ignored =>
                    string.Equals(ignored, text, StringComparison.OrdinalIgnoreCase)))
            {
                nonFinancial++;
                continue;
            }

            try
            {
                records.Add(Map(row, columns, version, values, unknownConcepts));
            }
            catch (ProfileValueException exception)
            {
                rejected.Add(new RejectedRecord(
                    Cell(row, columns, ImportField.NaturalId), row.Number, row.Raw, exception.Message));
            }
        }

        if (unknownConcepts.Count > 0)
        {
            // Se avisa una vez por concepto y no por fila: cien filas del mismo concepto
            // sin traducir son un solo hueco del perfil, no cien problemas.
            warnings.Add(
                $"El perfil no traduce estos conceptos, y sus movimientos entran sin clasificar: {string.Join(", ", unknownConcepts.Order(StringComparer.OrdinalIgnoreCase))}.");
        }

        return new ImportReadResult(records, rejected, nonFinancial, warnings);
    }

    /// <summary>
    /// Empareja los campos del perfil con las posiciones reales del fichero.
    /// </summary>
    /// <remarks>
    /// Por nombre de cabecera y no por posición: un bróker que inserta una columna en
    /// medio movería todas las demás, y por posición se importaría todo desplazado sin
    /// que nada fallara.
    /// </remarks>
    private static Dictionary<ImportField, int> ResolveColumns(
        ImportProfileVersion version,
        IReadOnlyList<string> headers)
    {
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index]?.Trim();

            if (!string.IsNullOrEmpty(header) && !positions.ContainsKey(header))
            {
                positions[header] = index;
            }
        }

        var resolved = new Dictionary<ImportField, int>();

        foreach (var (field, header) in version.Columns)
        {
            if (positions.TryGetValue(header, out var index))
            {
                resolved[field] = index;
            }
        }

        return resolved;
    }

    private static ImportRecord Map(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportProfileVersion version,
        ProfileValueReader values,
        HashSet<string> unknownConcepts)
    {
        var concept = Cell(row, columns, ImportField.Concept);
        var type = ResolveType(concept, version, unknownConcepts);

        var currencyCode = Cell(row, columns, ImportField.Currency) ?? version.FixedCurrency;

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new ProfileValueException("La fila no trae divisa y el perfil tampoco fija ninguna.");
        }

        var date = values.Date(Cell(row, columns, ImportField.Date));
        var amount = values.Decimal(Cell(row, columns, ImportField.GrossAmount), "importe");

        return new ImportRecord(
            NaturalId: Cell(row, columns, ImportField.NaturalId),
            RowNumber: row.Number,
            Type: type,
            AssetSymbol: Cell(row, columns, ImportField.AssetSymbol),
            AssetClass: null,
            Quantity: values.OptionalDecimal(Cell(row, columns, ImportField.Quantity)) ?? 0m,
            UnitPrice: values.OptionalDecimal(Cell(row, columns, ImportField.UnitPrice)),
            GrossAmount: amount,
            Currency: Currency.FromCode(currencyCode.Trim()),
            Fee: values.OptionalDecimal(Cell(row, columns, ImportField.Fee)) ?? 0m,
            Withholding: values.OptionalDecimal(Cell(row, columns, ImportField.Withholding)),
            OccurredAt: null,
            NaiveOccurredAt: date,
            SourceTimeZoneId: version.TimeZoneId,
            SplitRatio: values.OptionalDecimal(Cell(row, columns, ImportField.SplitRatio)),
            RawContent: row.Raw);
    }

    private static TransactionType ResolveType(
        string? concept,
        ImportProfileVersion version,
        HashSet<string> unknownConcepts)
    {
        if (string.IsNullOrWhiteSpace(concept))
        {
            return TransactionType.Unknown;
        }

        var text = concept.Trim();

        if (version.Concepts.TryGetValue(text, out var type))
        {
            return type;
        }

        // Sin traducción entra como desconocido, no bloquea el fichero. La revisión
        // recoge después estos movimientos, y el perfil se corrige una vez.
        unknownConcepts.Add(text);

        return TransactionType.Unknown;
    }

    private static string? Cell(
        TabularRow row,
        IReadOnlyDictionary<ImportField, int> columns,
        ImportField field)
    {
        if (!columns.TryGetValue(field, out var index))
        {
            return null;
        }

        var value = row.Cell(index)?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
