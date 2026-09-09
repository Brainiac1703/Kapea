using System.Text.Json;
using Kapea.Domain.Assets;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Import;

/// <summary>
/// Forma serializable de un registro normalizado.
/// </summary>
/// <remarks>
/// Es un tipo aparte y con campos primitivos a propósito: serializar los tipos de
/// valor del dominio ataría el formato guardado a su forma interna, y un cambio de
/// dominio dejaría ilegibles las importaciones a medio confirmar.
/// </remarks>
internal sealed record StagedPayload(
    string? NaturalId,
    int? RowNumber,
    TransactionType Type,
    string? AssetSymbol,
    AssetClass? AssetClass,
    decimal Quantity,
    decimal? UnitPrice,
    decimal GrossAmount,
    string Currency,
    decimal Fee,
    decimal? Withholding,
    DateTimeOffset? OccurredAt,
    DateTime? NaiveOccurredAt,
    string SourceTimeZoneId,
    decimal? SplitRatio)
{
    internal static string Serialize(ImportRecord record) =>
        JsonSerializer.Serialize(new StagedPayload(
            record.NaturalId,
            record.RowNumber,
            record.Type,
            record.AssetSymbol,
            record.AssetClass,
            record.Quantity,
            record.UnitPrice,
            record.GrossAmount,
            record.Currency.Code,
            record.Fee,
            record.Withholding,
            record.OccurredAt,
            record.NaiveOccurredAt,
            record.SourceTimeZoneId,
            record.SplitRatio));

    internal static ImportRecord Deserialize(string payload, string rawContent)
    {
        var stored = JsonSerializer.Deserialize<StagedPayload>(payload)
            ?? throw new InvalidOperationException("El registro almacenado no se puede leer.");

        return new ImportRecord(
            stored.NaturalId,
            stored.RowNumber,
            stored.Type,
            stored.AssetSymbol,
            stored.AssetClass,
            stored.Quantity,
            stored.UnitPrice,
            stored.GrossAmount,
            Kapea.Domain.ValueObjects.Currency.FromCode(stored.Currency),
            stored.Fee,
            stored.Withholding,
            stored.OccurredAt,
            stored.NaiveOccurredAt,
            stored.SourceTimeZoneId,
            stored.SplitRatio,
            rawContent);
    }
}
