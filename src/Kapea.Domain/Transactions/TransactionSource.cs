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
/// <param name="RawContent">Contenido original íntegro del registro. Ocupa poco a esta escala y es lo que sostiene la trazabilidad.</param>
/// <param name="ProfileId">Perfil con el que se leyó la fila. Nulo si el origen fue una API o un ajuste manual.</param>
/// <param name="ProfileVersion">Versión concreta de ese perfil, que es la que fija cómo se interpretó cada columna.</param>
public sealed record TransactionSource(
    Guid? ImportRunId,
    string? NaturalId,
    int? RowNumber,
    string Fingerprint,
    string? RawContent = null,
    Guid? ProfileId = null,
    int? ProfileVersion = null)
{
    public static TransactionSource FromImport(
        Guid importRunId,
        string? naturalId,
        int? rowNumber,
        string fingerprint,
        string? rawContent = null,
        Guid? profileId = null,
        int? profileVersion = null)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new DomainException("Un movimiento importado necesita huella de deduplicación.");
        }

        // Van juntos o no van: una versión sin perfil no dice nada, y un perfil sin
        // versión no permite recuperar las reglas que produjeron la cifra, que es lo
        // único para lo que se guarda.
        if (profileId.HasValue != profileVersion.HasValue)
        {
            throw new DomainException(
                "El perfil y su versión se guardan juntos: uno sin el otro no permite reconstruir cómo se interpretó la fila.");
        }

        return new TransactionSource(
            importRunId, naturalId, rowNumber, fingerprint, rawContent, profileId, profileVersion);
    }

    /// <summary>
    /// Origen de un movimiento apuntado a mano.
    /// </summary>
    /// <remarks>
    /// La huella lleva un prefijo propio y un identificador nuevo: nunca coincide con la de
    /// un importado, así que la deduplicación no puede confundir un apunte con un registro
    /// de la plataforma. Esa coincidencia se busca aparte, por los datos.
    /// </remarks>
    public static TransactionSource ForManualEntry(Guid entryId) =>
        new(ImportRunId: null, NaturalId: null, RowNumber: null, Fingerprint: $"entry:{entryId:N}", RawContent: null);

    public static TransactionSource ForManualAdjustment(Guid adjustmentId) =>
        new(ImportRunId: null, NaturalId: null, RowNumber: null, Fingerprint: $"manual:{adjustmentId:N}", RawContent: null);
}
