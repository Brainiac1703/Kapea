using Kapea.Domain.Common;

namespace Kapea.Domain.Transactions;

/// <summary>
/// Referencia al registro del que salió el movimiento. Es lo que sostiene la
/// trazabilidad exigida por un informe presentable a una gestoría: de la cifra al
/// movimiento, del movimiento a la ejecución de importación y de ahí a la fila o
/// respuesta original.
/// </summary>
/// <param name="ImportRunId">Ejecución de importación. Nulo en un ajuste manual.</param>
/// <param name="NaturalId">Identificador que aporta el origen (txid de Kraken, id de operación). Nulo en ficheros.</param>
/// <param name="RowNumber">Fila dentro del fichero. Nulo en orígenes de API.</param>
/// <param name="Fingerprint">Huella de deduplicación, estable para el mismo registro de origen.</param>
public sealed record TransactionSource(Guid? ImportRunId, string? NaturalId, int? RowNumber, string Fingerprint)
{
    public static TransactionSource FromImport(Guid importRunId, string? naturalId, int? rowNumber, string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new DomainException("Un movimiento importado necesita huella de deduplicación.");
        }

        return new TransactionSource(importRunId, naturalId, rowNumber, fingerprint);
    }

    public static TransactionSource ForManualAdjustment(Guid adjustmentId) =>
        new(ImportRunId: null, NaturalId: null, RowNumber: null, Fingerprint: $"manual:{adjustmentId:N}");
}
