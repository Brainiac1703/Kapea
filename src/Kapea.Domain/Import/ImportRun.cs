using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Import;

/// <summary>Estado de una ejecución de importación.</summary>
public enum ImportRunStatus
{
    /// <summary>Registros normalizados y clasificados, todavía sin escribir en el dominio.</summary>
    Staged = 1,

    /// <summary>Confirmada: sus movimientos están persistidos.</summary>
    Completed = 2,

    /// <summary>Ha fallado. No ha dejado ningún movimiento.</summary>
    Failed = 3,

    /// <summary>El usuario abandonó la vista previa sin confirmar.</summary>
    Discarded = 4,
}

/// <summary>Cómo terminó cada registro leído del origen.</summary>
public enum StagedRecordOutcome
{
    /// <summary>Se puede importar.</summary>
    Importable = 1,

    /// <summary>Su huella ya existe en la cuenta: reimportación de algo ya presente.</summary>
    Duplicate = 2,

    /// <summary>No se ha podido normalizar. Se conserva con su motivo para poder reprocesarlo.</summary>
    Rejected = 3,

    /// <summary>Apunte informativo sin efecto financiero.</summary>
    NonFinancial = 4,
}

/// <summary>
/// Registro del origen tal y como quedó tras normalizarlo y clasificarlo, antes de
/// escribir nada en las tablas de dominio.
/// </summary>
/// <remarks>
/// Esta fase intermedia existe para que XTB pueda enseñar una vista previa y los
/// adaptadores de API confirmen automáticamente: un solo camino, dos formas de
/// confirmarlo. También es lo que permite reprocesar un rechazo sin volver al origen.
/// </remarks>
public sealed class StagedRecord
{
    private StagedRecord()
    {
        Fingerprint = null!;
        RawContent = null!;
        Payload = null!;
    }

    public StagedRecord(
        Guid importRunId,
        StagedRecordOutcome outcome,
        string fingerprint,
        string? naturalId,
        int? rowNumber,
        string rawContent,
        string payload,
        string? rejectionReason)
    {
        Id = Guid.NewGuid();
        ImportRunId = importRunId;
        Outcome = outcome;
        Fingerprint = fingerprint;
        NaturalId = naturalId;
        RowNumber = rowNumber;
        RawContent = rawContent;
        Payload = payload;
        RejectionReason = rejectionReason;
    }

    public Guid Id { get; private set; }

    public Guid ImportRunId { get; private set; }

    public StagedRecordOutcome Outcome { get; private set; }

    public string Fingerprint { get; private set; }

    public string? NaturalId { get; private set; }

    public int? RowNumber { get; private set; }

    /// <summary>Contenido original íntegro: la fila del fichero o el fragmento de respuesta.</summary>
    public string RawContent { get; private set; }

    /// <summary>Registro ya normalizado, serializado, para poder materializarlo al confirmar.</summary>
    public string Payload { get; private set; }

    public string? RejectionReason { get; private set; }

    public void Reclassify(StagedRecordOutcome outcome, string? rejectionReason = null)
    {
        Outcome = outcome;
        RejectionReason = outcome == StagedRecordOutcome.Rejected ? rejectionReason : null;
    }
}

/// <summary>
/// Ejecución de importación. Es lo que hace observable el proceso: qué se leyó, qué
/// entró, qué se descartó por duplicado y qué se rechazó, con su motivo.
/// </summary>
public sealed class ImportRun
{
    private readonly List<StagedRecord> _records = [];

    private ImportRun()
    {
    }

    private ImportRun(
        Guid id,
        UserId userId,
        Guid accountId,
        Platform platform,
        string? fileName,
        DateTimeOffset startedAt)
    {
        Id = id;
        UserId = userId;
        AccountId = accountId;
        Platform = platform;
        FileName = fileName;
        StartedAt = startedAt;
        Status = ImportRunStatus.Staged;
    }

    public Guid Id { get; private set; }

    public UserId UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Platform Platform { get; private set; }

    /// <summary>Nombre del fichero subido. Nulo en importaciones de API.</summary>
    public string? FileName { get; private set; }

    public ImportRunStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>Instante hasta el que llega esta importación, para la siguiente sincronización incremental.</summary>
    public DateTimeOffset? CoversUntil { get; private set; }

    public IReadOnlyList<StagedRecord> Records => _records;

    public int RecordsRead => _records.Count;

    public int RecordsImported => Count(StagedRecordOutcome.Importable);

    public int DuplicatesDiscarded => Count(StagedRecordOutcome.Duplicate);

    public int RecordsRejected => Count(StagedRecordOutcome.Rejected);

    /// <summary>
    /// Apuntes sin efecto financiero que el adaptador descartó. Se cuentan pero no se
    /// guardan uno a uno: un descarte silencioso sería indistinguible de un dato
    /// perdido, y el recuento basta para que se vea.
    /// </summary>
    public int NonFinancialRecords { get; private set; }

    public static ImportRun Start(
        UserId userId,
        Guid accountId,
        Platform platform,
        DateTimeOffset startedAt,
        string? fileName = null) =>
        new(Guid.NewGuid(), userId, accountId, platform, fileName, startedAt);

    public void Stage(StagedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureStaged();

        _records.Add(record);
    }

    public void CountNonFinancial(int count)
    {
        EnsureStaged();

        NonFinancialRecords = count;
    }

    public void Complete(DateTimeOffset completedAt, DateTimeOffset? coversUntil = null)
    {
        EnsureStaged();

        Status = ImportRunStatus.Completed;
        CompletedAt = completedAt;
        CoversUntil = coversUntil;
    }

    /// <summary>
    /// Marca la ejecución como fallida. No lleva registros importados: la confirmación
    /// es atómica, así que un fallo a mitad no deja movimientos sueltos.
    /// </summary>
    public void Fail(string reason, DateTimeOffset failedAt)
    {
        Status = ImportRunStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Sin motivo indicado." : reason.Trim();
        CompletedAt = failedAt;
    }

    public void Discard(DateTimeOffset discardedAt)
    {
        EnsureStaged();

        Status = ImportRunStatus.Discarded;
        CompletedAt = discardedAt;
    }

    private int Count(StagedRecordOutcome outcome) =>
        _records.Count(record => record.Outcome == outcome);

    private void EnsureStaged()
    {
        if (Status != ImportRunStatus.Staged)
        {
            throw new DomainException($"La ejecución de importación ya está {Status} y no admite cambios.");
        }
    }
}
