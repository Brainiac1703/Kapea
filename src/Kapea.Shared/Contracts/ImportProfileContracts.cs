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
