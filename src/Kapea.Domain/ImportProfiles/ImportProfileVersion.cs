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
    private readonly List<string> _fiatCurrencies = [];

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

    /// <summary>Qué representa cada fila: un apunte, o una posición entera.</summary>
    public RowShape RowShape { get; private set; } = RowShape.SingleMovement;

    /// <summary>De dónde sale el importe: de su columna, o de multiplicar cantidad por precio.</summary>
    public AmountSource AmountSource { get; private set; } = AmountSource.Column;

    /// <summary>Clase de activo cuando el informe no la trae y todo el fichero es de la misma.</summary>
    public string? FixedAssetClass { get; private set; }

    /// <summary>
    /// El importe se guarda siempre en positivo.
    /// </summary>
    /// <remarks>
    /// Muchos extractos marcan las salidas con signo negativo. La dirección del dinero
    /// ya la lleva el tipo del movimiento, así que conservar además el signo la contaría
    /// dos veces y una retirada restaría donde debía sumar.
    /// </remarks>
    public bool AmountIsAlwaysPositive { get; private set; }

    /// <summary>
    /// Monedas que son dinero y no activos.
    /// </summary>
    /// <remarks>
    /// En un intercambio deciden qué se compra y qué se vende. Se declaran en el perfil
    /// y no se deducen: una stablecoin se comporta como dinero para quien la usa así, y
    /// como un activo más para quien la declara. Esa es una decisión de quien importa,
    /// no del programa.
    /// </remarks>
    public IReadOnlyList<string> FiatCurrencies =>
        _fiatCurrencies.Count > 0 ? new ReadOnlyCollection<string>(_fiatCurrencies) : DefaultFiat;

    private static readonly IReadOnlyList<string> DefaultFiat = ["EUR", "USD", "GBP", "CHF"];

    public bool IsFiat(string? currency) =>
        currency is { Length: > 0 }
        && FiatCurrencies.Any(fiat => string.Equals(fiat, currency, StringComparison.OrdinalIgnoreCase));

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
        string? fixedCurrency = null,
        RowShape rowShape = RowShape.SingleMovement,
        AmountSource amountSource = AmountSource.Column,
        string? fixedAssetClass = null,
        bool amountIsAlwaysPositive = false,
        IEnumerable<string>? fiatCurrencies = null)
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
            RowShape = rowShape,
            AmountSource = amountSource,
            FixedAssetClass = string.IsNullOrWhiteSpace(fixedAssetClass) ? null : fixedAssetClass.Trim(),
            AmountIsAlwaysPositive = amountIsAlwaysPositive,
        };

        version._recognizedHeaders.AddRange(recognizedHeaders.Where(header => !string.IsNullOrWhiteSpace(header)).Select(header => header.Trim()));
        version._dateFormats.AddRange(dateFormats.Where(format => !string.IsNullOrWhiteSpace(format)).Select(format => format.Trim()));
        version._fiatCurrencies.AddRange(
            (fiatCurrencies ?? []).Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpperInvariant()));

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

        foreach (var required in RequiredFields())
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

        if (RowShape == RowShape.ExchangePair)
        {
            // El par ya trae sus monedas: exigir además una columna de divisa dejaría
            // fuera el formato para el que existe esta forma.
            return;
        }

        if (!_columns.ContainsKey(ImportField.Currency) && FixedCurrency is null)
        {
            throw new DomainException(
                "El perfil no dice la divisa ni qué columna la trae: una cifra sin divisa no se puede convertir a euros.");
        }
    }

    /// <summary>
    /// Qué columnas necesita este perfil según lo que represente cada fila.
    /// </summary>
    /// <remarks>
    /// Un extracto de efectivo necesita fecha e importe. Un informe de posiciones no
    /// trae ninguna de las dos: trae apertura, cierre, volumen y precios, y el importe
    /// sale de multiplicar. Exigirle las mismas columnas lo rechazaría siempre.
    /// </remarks>
    private IEnumerable<ImportField> RequiredFields()
    {
        switch (RowShape)
        {
            case RowShape.OpenPosition:
                yield return ImportField.OpenDate;
                yield return ImportField.Quantity;
                yield return ImportField.OpenPrice;

                break;

            case RowShape.ExchangePair:
                yield return ImportField.Date;
                yield return ImportField.DestinationAmount;
                yield return ImportField.DestinationCurrency;

                break;

            case RowShape.OpenAndClosePosition:
                yield return ImportField.OpenDate;
                yield return ImportField.Quantity;
                yield return ImportField.OpenPrice;
                yield return ImportField.CloseDate;
                yield return ImportField.ClosePrice;

                break;

            default:
                yield return ImportField.Date;

                if (AmountSource == AmountSource.Column)
                {
                    yield return ImportField.GrossAmount;
                }
                else
                {
                    yield return ImportField.Quantity;
                    yield return ImportField.UnitPrice;
                }

                break;
        }
    }

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
        ImportField.DestinationAmount => "la cantidad que entra",
        ImportField.DestinationCurrency => "la moneda que entra",
        _ => field.ToString(),
    };
}
