using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;

namespace Kapea.Application.Import;

/// <summary>
/// Lo que se envía para que alguien deduzca cómo se lee un fichero.
/// </summary>
/// <remarks>
/// Cabeceras y unas pocas filas, nada más. Un extracto es un dato personal, y para
/// deducir qué columna es la fecha no hace falta ver el año entero.
/// </remarks>
public sealed record MappingSample(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows)
{
    /// <summary>Tope de filas que salen de aquí. No es una preferencia, es el límite.</summary>
    public const int MaximumRows = 3;

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } =
        [.. (Rows ?? []).Take(MaximumRows)];
}

/// <param name="Confidence">Cuánta seguridad declara quien lo propone, de 0 a 1.</param>
public sealed record FieldProposal(ImportField Field, string Column, double Confidence);

public sealed record ConceptProposal(string Concept, TransactionType Type, double Confidence);

/// <summary>
/// Cómo se propone leer el fichero.
/// </summary>
/// <remarks>
/// Es un mapeo, nunca una cifra. Lo que llega de fuera dice qué columna es cuál; los
/// importes los calcula después el motor determinista aplicando el perfil, así que
/// recalcular un ejercicio ya presentado da siempre lo mismo.
/// </remarks>
public sealed record MappingProposal(
    IReadOnlyList<FieldProposal> Fields,
    IReadOnlyList<ConceptProposal> Concepts,
    DecimalConvention DecimalConvention,
    IReadOnlyList<string> DateFormats,
    RowShape RowShape,
    AmountSource AmountSource,
    char Delimiter,
    string? FixedCurrency)
{
    public string? ColumnFor(ImportField field) =>
        Fields.FirstOrDefault(proposal => proposal.Field == field)?.Column;

    public double ConfidenceFor(ImportField field) =>
        Fields.FirstOrDefault(proposal => proposal.Field == field)?.Confidence ?? 0d;
}

/// <summary>Propone cómo leer un formato que Kapea todavía no conoce.</summary>
public interface IMappingProposer
{
    /// <summary>Está configurado y disponible. Sin él, el mapeo se hace a mano.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Devuelve una propuesta, o nula si no se ha podido obtener.
    /// </summary>
    /// <remarks>
    /// Nula y no una excepción: que el servicio no esté no es un error de la
    /// importación, es que ese atajo no está disponible. La vía manual sigue existiendo.
    /// </remarks>
    Task<MappingProposal?> ProposeAsync(
        PlatformCode platform,
        MappingSample sample,
        CancellationToken cancellationToken = default);
}
