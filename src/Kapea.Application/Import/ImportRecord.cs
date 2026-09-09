using Kapea.Domain.Assets;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Import;

/// <summary>
/// Registro ya normalizado por un adaptador, todavía sin persistir. Es el único tipo
/// que el motor de importación conoce, y por eso a partir de aquí da igual si el
/// origen fue un fichero de XTB o la API de Kraken.
/// </summary>
/// <param name="NaturalId">Identificador que aporta el origen. Nulo en ficheros que no lo traen.</param>
/// <param name="RowNumber">Fila dentro del fichero. Nulo en orígenes de API.</param>
/// <param name="AssetSymbol">Símbolo tal y como lo da el origen, ya traducido al canónico si se pudo.</param>
/// <param name="OccurredAt">Instante con desfase, cuando el origen lo aporta.</param>
/// <param name="NaiveOccurredAt">Fecha sin zona, cuando el origen no la aporta. Se interpreta en SourceTimeZoneId.</param>
/// <param name="SplitRatio">Proporción del split. Solo en registros de tipo Split.</param>
/// <param name="RawContent">Contenido original íntegro: la fila o el fragmento de respuesta del que salió.</param>
public sealed record ImportRecord(
    string? NaturalId,
    int? RowNumber,
    TransactionType Type,
    string? AssetSymbol,
    AssetClass? AssetClass,
    decimal Quantity,
    decimal? UnitPrice,
    decimal GrossAmount,
    Currency Currency,
    decimal Fee,
    decimal? Withholding,
    DateTimeOffset? OccurredAt,
    DateTime? NaiveOccurredAt,
    string SourceTimeZoneId,
    decimal? SplitRatio,
    string RawContent)
{
    /// <summary>Resuelve el instante del registro, venga con zona o sin ella.</summary>
    public Occurrence ToOccurrence() => OccurredAt is { } instant
        ? Occurrence.FromOffset(instant, SourceTimeZoneId)
        : Occurrence.FromNaive(
            NaiveOccurredAt ?? throw new InvalidOperationException("El registro no trae fecha."),
            SourceTimeZoneId);
}

/// <summary>
/// Registro que el adaptador no ha podido normalizar. Se conserva con su contenido
/// original para que el usuario pueda verlo, entender por qué falló y reprocesarlo
/// cuando la causa esté corregida.
/// </summary>
public sealed record RejectedRecord(string? NaturalId, int? RowNumber, string RawContent, string Reason);

/// <summary>
/// Lo que devuelve un adaptador: lo que ha entendido, lo que no, y lo que ha
/// descartado por no tener efecto financiero. Los tres recuentos se enseñan al
/// usuario, porque un descarte silencioso es indistinguible de un dato perdido.
/// </summary>
public sealed record ImportReadResult(
    IReadOnlyList<ImportRecord> Records,
    IReadOnlyList<RejectedRecord> Rejected,
    int NonFinancialRecordCount = 0,
    IReadOnlyList<string>? Warnings = null)
{
    public static ImportReadResult Empty { get; } = new([], []);

    public IReadOnlyList<string> Warnings { get; init; } = Warnings ?? [];
}
