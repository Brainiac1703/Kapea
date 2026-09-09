using System.Collections.ObjectModel;
using Kapea.Domain.Common;
using Kapea.Domain.Transactions;

namespace Kapea.Domain.ImportProfiles;

/// <summary>
/// Reglas con las que se lee un fichero: una versión concreta de un perfil.
/// </summary>
/// <remarks>
/// Es inmutable. Corregir un perfil no reescribe estas reglas, crea otra versión: cada
/// movimiento guarda con cuál se interpretó, y reescribirlas dejaría movimientos
/// apuntando a unas reglas que ya no son las que produjeron sus cifras.
/// </remarks>
public sealed class ImportProfileVersion
{
    private readonly List<string> _recognizedHeaders = [];
    private readonly Dictionary<ImportField, string> _columns = [];
    private readonly Dictionary<string, TransactionType> _concepts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _nonFinancialConcepts = [];
    private readonly List<string> _dateFormats = [];

    private ImportProfileVersion(Guid id, int number, DateTimeOffset createdAt)
    {
        Id = id;
        Number = number;
        CreatedAt = createdAt;
    }

    private ImportProfileVersion()
    {
        // Para el materializador de EF Core.
    }

    public Guid Id { get; private set; }

    public int Number { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public char Delimiter { get; private set; } = ';';

    public DecimalConvention DecimalConvention { get; private set; } = DecimalConvention.European;

    /// <summary>Zona con la que se interpreta una fecha sin desfase, que es lo que trae casi todo CSV.</summary>
    public string TimeZoneId { get; private set; } = "Europe/Madrid";

    /// <summary>Divisa cuando el fichero no trae columna de divisa. Nula si sí la trae.</summary>
    public string? FixedCurrency { get; private set; }

    /// <summary>Cabeceras por las que se reconoce un fichero como de este perfil.</summary>
    public IReadOnlyList<string> RecognizedHeaders => new ReadOnlyCollection<string>(_recognizedHeaders);

    /// <summary>Qué columna alimenta cada campo.</summary>
    public IReadOnlyDictionary<ImportField, string> Columns => _columns;

    /// <summary>Qué tipo de movimiento significa cada concepto del origen.</summary>
    public IReadOnlyDictionary<string, TransactionType> Concepts => _concepts;

    /// <summary>Conceptos sin efecto financiero. Se cuentan y se descartan, no se rechazan.</summary>
    public IReadOnlyList<string> NonFinancialConcepts => new ReadOnlyCollection<string>(_nonFinancialConcepts);

    /// <summary>Formatos con los que se intenta leer la fecha, en orden.</summary>
    public IReadOnlyList<string> DateFormats => new ReadOnlyCollection<string>(_dateFormats);

    public static ImportProfileVersion Create(
        int number,
        DateTimeOffset createdAt,
        char delimiter,
        DecimalConvention decimalConvention,
        string timeZoneId,
        IEnumerable<string> recognizedHeaders,
        IReadOnlyDictionary<ImportField, string> columns,
        IEnumerable<string> dateFormats,
        IReadOnlyDictionary<string, TransactionType>? concepts = null,
        IEnumerable<string>? nonFinancialConcepts = null,
        string? fixedCurrency = null)
    {
        ArgumentNullException.ThrowIfNull(recognizedHeaders);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(dateFormats);

        var version = new ImportProfileVersion(Guid.NewGuid(), number, createdAt)
        {
            Delimiter = delimiter,
            DecimalConvention = decimalConvention,
            TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "Europe/Madrid" : timeZoneId.Trim(),
            FixedCurrency = string.IsNullOrWhiteSpace(fixedCurrency) ? null : fixedCurrency.Trim().ToUpperInvariant(),
        };

        version._recognizedHeaders.AddRange(recognizedHeaders.Where(header => !string.IsNullOrWhiteSpace(header)).Select(header => header.Trim()));
        version._dateFormats.AddRange(dateFormats.Where(format => !string.IsNullOrWhiteSpace(format)).Select(format => format.Trim()));
        version._nonFinancialConcepts.AddRange(
            (nonFinancialConcepts ?? []).Where(concept => !string.IsNullOrWhiteSpace(concept)).Select(concept => concept.Trim()));

        foreach (var (field, column) in columns.Where(entry => !string.IsNullOrWhiteSpace(entry.Value)))
        {
            version._columns[field] = column.Trim();
        }

        foreach (var (concept, type) in concepts ?? new Dictionary<string, TransactionType>())
        {
            if (!string.IsNullOrWhiteSpace(concept))
            {
                version._concepts[concept.Trim()] = type;
            }
        }

        version.EnsureUsable();

        return version;
    }

    /// <summary>
    /// Comprueba que estas reglas alcanzan para leer un fichero.
    /// </summary>
    /// <remarks>
    /// Se valida al crear y no al importar: un perfil incompleto guardado es una
    /// importación que fallará dentro de un mes, cuando nadie recuerde qué se mapeó.
    /// </remarks>
    public void EnsureUsable()
    {
        if (_recognizedHeaders.Count == 0)
        {
            throw new DomainException(
                "El perfil necesita al menos una cabecera por la que reconocer sus ficheros.");
        }

        foreach (var required in new[] { ImportField.Date, ImportField.GrossAmount })
        {
            if (!_columns.ContainsKey(required))
            {
                throw new DomainException(
                    $"El perfil no dice qué columna es {Describe(required)}, y sin eso no se puede interpretar ninguna fila.");
            }
        }

        if (_dateFormats.Count == 0)
        {
            throw new DomainException(
                "El perfil no dice en qué formato vienen las fechas, y una fecha ambigua se leería mal sin avisar.");
        }

        if (!_columns.ContainsKey(ImportField.Currency) && FixedCurrency is null)
        {
            throw new DomainException(
                "El perfil no dice la divisa ni qué columna la trae: una cifra sin divisa no se puede convertir a euros.");
        }
    }

    private static string Describe(ImportField field) => field switch
    {
        ImportField.Date => "la fecha",
        ImportField.GrossAmount => "el importe",
        _ => field.ToString(),
    };
}
