namespace Kapea.Shared.Contracts;

/// <summary>
/// Perfil de importación con todas sus versiones.
/// </summary>
/// <remarks>
/// Viajan todas y no solo la vigente porque la pantalla tiene que poder enseñar con qué
/// reglas se leyó una importación antigua, y esas reglas son justamente las que ya no
/// están en uso.
/// </remarks>
public sealed record ImportProfileResponse(
    Guid Id,
    string Platform,
    string Name,
    bool BuiltIn,
    int CurrentVersion,
    IReadOnlyList<ImportProfileVersionResponse> Versions);

/// <summary>Las reglas de una versión concreta. Una vez guardada no cambia nunca.</summary>
public sealed record ImportProfileVersionResponse(
    int Number,
    DateTimeOffset CreatedAt,
    string Delimiter,
    string DecimalConvention,
    string TimeZoneId,
    string? FixedCurrency,
    string RowShape,
    string AmountSource,
    string? FixedAssetClass,
    bool AmountIsAlwaysPositive,
    IReadOnlyList<string> RecognizedHeaders,
    IReadOnlyList<string> DateFormats,
    IReadOnlyList<string> NonFinancialConcepts,
    IReadOnlyDictionary<string, string> Columns,
    IReadOnlyDictionary<string, string> Concepts);

/// <summary>Las reglas que se envían al crear un perfil o al corregirlo.</summary>
public sealed record ImportProfileRulesRequest(
    string Delimiter,
    string DecimalConvention,
    string TimeZoneId,
    string? FixedCurrency,
    string RowShape,
    string AmountSource,
    string? FixedAssetClass,
    bool AmountIsAlwaysPositive,
    IReadOnlyList<string> RecognizedHeaders,
    IReadOnlyList<string> DateFormats,
    IReadOnlyList<string> NonFinancialConcepts,
    IReadOnlyDictionary<string, string> Columns,
    IReadOnlyDictionary<string, string> Concepts);

public sealed record CreateImportProfileRequest(string Platform, string Name, ImportProfileRulesRequest Rules);

/// <summary>Corrección de un perfil. Guarda una versión nueva y conserva la anterior.</summary>
public sealed record ReviseImportProfileRequest(string? Name, ImportProfileRulesRequest Rules);

/// <summary>Los campos del movimiento a los que puede apuntar una columna, y su nombre en pantalla.</summary>
public sealed record ImportFieldResponse(string Field, string Label, bool Required);

/// <summary>
/// Lo que trae un fichero, mirado sin importar nada.
/// </summary>
/// <remarks>
/// Sirve para dar de alta el perfil de un formato desconocido: hace falta ver las
/// columnas antes de poder decir cuál es cuál.
/// </remarks>
public sealed record FileInspectionResponse(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    Guid? MatchedProfileId,
    string? MatchedProfileName,
    bool ProposalsAvailable);

/// <summary>Cómo se propone leer el fichero, y si hay motivos para revisarlo antes de aplicarlo.</summary>
public sealed record MappingProposalResponse(
    IReadOnlyDictionary<string, string> Columns,
    IReadOnlyDictionary<string, double> Confidence,
    IReadOnlyDictionary<string, string> Concepts,
    string DecimalConvention,
    IReadOnlyList<string> DateFormats,
    string RowShape,
    string AmountSource,
    string Delimiter,
    string? FixedCurrency,
    bool IsConclusive,
    IReadOnlyList<string> Doubts);

/// <summary>Lo que se envía para pedir una propuesta: cabeceras y como mucho tres filas.</summary>
public sealed record MappingSampleRequest(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);

