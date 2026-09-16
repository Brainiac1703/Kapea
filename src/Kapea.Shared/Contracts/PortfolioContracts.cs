using System.Text.Json.Serialization;

namespace Kapea.Shared.Contracts;

/// <summary>Cuenta del usuario en una plataforma.</summary>
public sealed record AccountResponse(Guid Id, string Platform, string Alias, string BaseCurrency);

/// <summary>
/// Plataforma del catálogo.
/// </summary>
/// <remarks>
/// La lista la da el servidor y no la trae escrita el cliente: dar de alta un bróker
/// que exporta un fichero es añadir una fila, y ninguna pantalla debería necesitar una
/// versión nueva para verla.
/// </remarks>
public sealed record PlatformResponse(string Code, string Name, string ImportKind, bool BuiltIn);

/// <summary>Alta de una plataforma que no viene de serie.</summary>
public sealed record CreatePlatformRequest(string Code, string Name, string ImportKind);

public sealed record CreateAccountRequest(string Platform, string Alias, string BaseCurrency);

/// <summary>Ejecución de importación con sus recuentos, tal y como la ve el usuario.</summary>
public sealed record ImportRunResponse(
    Guid Id,
    Guid AccountId,
    string Platform,
    string? FileName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int RecordsRead,
    int RecordsImported,
    int DuplicatesDiscarded,
    int RecordsRejected,
    int NonFinancialRecords,
    string? FailureReason,
    IReadOnlyList<RejectedRecordResponse> Rejected,
    string? ProfileName = null,
    int? ProfileVersion = null,
    IReadOnlyList<InterpretedRowResponse>? Sample = null,
    IReadOnlyList<ManualMatchResponse>? ManualMatches = null)
{
    /// <summary>Las primeras filas ya interpretadas. Vacía en importaciones de API.</summary>
    public IReadOnlyList<InterpretedRowResponse> Sample { get; init; } = Sample ?? [];

    /// <summary>Filas que coinciden con un apunte manual de la cuenta. Vacía si no hay ninguna.</summary>
    public IReadOnlyList<ManualMatchResponse> ManualMatches { get; init; } = ManualMatches ?? [];
}

/// <summary>
/// Una fila del fichero ya interpretada, tal y como entraría.
/// </summary>
/// <remarks>
/// Es la contrapartida de aplicar un perfil sin preguntar. Un mapeo equivocado —la
/// columna de la comisión tomada por el importe— produce cifras plausibles, y solo se
/// ve mirando filas concretas antes de confirmar.
/// </remarks>
public sealed record InterpretedRowResponse(
    int? RowNumber,
    DateTimeOffset OccurredAt,
    string Type,
    string? AssetSymbol,
    decimal Quantity,
    decimal GrossAmount,
    string Currency,
    decimal Fee,
    string Outcome);

/// <summary>Registro rechazado con su contenido original y el motivo, para poder corregirlo.</summary>
public sealed record RejectedRecordResponse(Guid Id, int? RowNumber, string? NaturalId, string RawContent, string Reason);

/// <summary>
/// De dónde viene un movimiento, tal como se enseña.
/// </summary>
/// <remarks>
/// Distingue API de fichero, cosa que el dominio no hace porque para el cálculo da igual;
/// para quien revisa una cifra no: un fichero se puede volver a mirar, una API no.
/// </remarks>
public static class MovementOrigins
{
    public const string Api = "Api";
    public const string File = "File";
    public const string Manual = "Manual";
    public const string Adjustment = "ManualAdjustment";
}

/// <summary>Movimiento normalizado con su rastro hasta el origen.</summary>
public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    Guid? AssetId,
    string? AssetSymbol,
    string Type,
    decimal Quantity,
    decimal? UnitPrice,
    decimal GrossAmount,
    string Currency,
    decimal Fee,
    DateTimeOffset OccurredAt,
    string OccurredAtTimeZoneId,
    string Origin,
    bool RequiresReview,
    Guid? ImportRunId,
    string? SourceNaturalId,
    int? SourceRowNumber,
    string? RawContent,
    decimal? AppliedExchangeRate,
    DateOnly? ExchangeRateDate,
    bool ExchangeRateWasSubstituted,
    Guid? ProfileId = null,
    string? ProfileName = null,
    int? ProfileVersion = null,
    string? Note = null,
    DateTimeOffset? RegisteredAt = null,
    DateTimeOffset? RevisedAt = null,
    string? AdjustmentReason = null,
    DateTimeOffset? VoidedAt = null,
    string? VoidReason = null,
    string? ImportFileName = null)
{
    public bool IsVoided => VoidedAt is not null;
}

/// <summary>Posición abierta. El valor de mercado puede faltar y se dice explícitamente.</summary>
public sealed record OpenPositionResponse(
    Guid AssetId,
    string AssetSymbol,
    bool AssetIsVerified,
    decimal Quantity,
    decimal CostInEuros,
    decimal AverageCostInEuros,
    decimal? MarketPriceInEuros,
    decimal? MarketValueInEuros,
    decimal? UnrealisedResultInEuros,
    DateTimeOffset? PriceAsOf,
    string AssetClass,
    decimal FeesInEuros,
    decimal RealizedResultInEuros,
    decimal? Weight,
    decimal? TargetInEuros = null,
    decimal? StopLossInEuros = null,
    bool ReachedTarget = false,
    bool ReachedStopLoss = false);

/// <summary>Las posiciones de una clase de activo con sus subtotales.</summary>
public sealed record PortfolioGroupResponse(
    string AssetClass,
    IReadOnlyList<OpenPositionResponse> Positions,
    decimal CostInEuros,
    decimal MarketValueInEuros,
    decimal? Weight);

/// <summary>Dinero disponible en una cuenta y una divisa.</summary>
public sealed record CashBalanceResponse(Guid AccountId, string AccountAlias, string Currency, decimal Amount);

/// <summary>Rendimientos cobrados de una clase de activo, con su retención.</summary>
public sealed record IncomeByClassResponse(
    string AssetClass,
    decimal GrossInEuros,
    decimal WithholdingInEuros,
    decimal NetInEuros);

/// <summary>Lo que pesa un activo y si se ha pasado de su tope.</summary>
public sealed record RiskWeightResponse(string Name, decimal ValueInEuros, decimal Share, bool OverCap);

/// <summary>
/// Aviso de que unos pocos activos concentran demasiado.
/// </summary>
/// <param name="Top">Los que más pesan, del mayor al menor.</param>
public sealed record ConcentrationResponse(
    IReadOnlyList<RiskWeightResponse> Top,
    decimal Share,
    decimal Threshold);

/// <summary>Cartera completa, con la advertencia visible cuando las cifras están incompletas.</summary>
public sealed record PortfolioResponse(
    IReadOnlyList<PortfolioGroupResponse> Groups,
    IReadOnlyList<CashBalanceResponse> Cash,
    decimal CashTotalInEuros,
    IReadOnlyList<string> CurrenciesWithoutRate,
    decimal TotalCostInEuros,
    decimal TotalMarketValueInEuros,
    decimal WealthInEuros,
    decimal RealizedResultInEuros,
    decimal UnrealisedResultInEuros,
    IReadOnlyList<IncomeByClassResponse> Income,
    int UnclassifiedTransactionCount,
    int PendingTransferCount,
    IReadOnlyList<string> Inconsistencies,
    bool MissingPrices,
    bool MissingCash,
    ConcentrationResponse? Concentration = null,
    IReadOnlyList<RiskWeightResponse>? Weights = null)
{
    /// <summary>Lo que pesa cada activo, para poder ver de un golpe si algo se ha disparado.</summary>
    public IReadOnlyList<RiskWeightResponse> Weights { get; init; } = Weights ?? [];

    /// <summary>
    /// Todas las posiciones seguidas, sin agrupar.
    /// </summary>
    /// <remarks>
    /// No viaja: se calcula de los grupos al leerla. Mandarla además de los grupos
    /// duplicaría la cartera entera en cada respuesta.
    /// </remarks>
    [JsonIgnore]
    public IEnumerable<OpenPositionResponse> Positions => Groups.SelectMany(group => group.Positions);

    /// <summary>Mientras haya movimientos sin resolver, estas cifras no están completas.</summary>
    public bool IsComplete => UnclassifiedTransactionCount == 0
        && PendingTransferCount == 0
        && Inconsistencies.Count == 0
        && !MissingPrices
        && !MissingCash;
}

/// <summary>Resultado realizado con su desglose hasta los lotes que lo originan.</summary>
public sealed record RealizedResultResponse(
    Guid DisposalTransactionId,
    Guid AssetId,
    string AssetSymbol,
    DateTimeOffset DisposedAt,
    int TaxYear,
    decimal Quantity,
    decimal ProceedsInEuros,
    decimal AcquisitionCostInEuros,
    decimal ResultInEuros,
    IReadOnlyList<ConsumedLotResponse> ConsumedLots,
    string? DisposalOrigin = null);

public sealed record ConsumedLotResponse(
    Guid LotId,
    Guid AcquisitionTransactionId,
    DateTimeOffset AcquiredAt,
    decimal Quantity,
    decimal AcquisitionCostInEuros,
    decimal ProceedsInEuros,
    decimal ResultInEuros,
    string? AcquisitionOrigin = null);

/// <summary>Resultados de un ejercicio, agregados por activo y con su total.</summary>
public sealed record TaxYearResultsResponse(
    int TaxYear,
    IReadOnlyList<AssetResultResponse> ByAsset,
    decimal TotalResultInEuros,
    IReadOnlyList<RealizedResultResponse> Details,
    bool IsComplete);

public sealed record AssetResultResponse(
    Guid AssetId,
    string AssetSymbol,
    decimal ProceedsInEuros,
    decimal AcquisitionCostInEuros,
    decimal ResultInEuros);

/// <summary>
/// Cuánto queda por revisar, para avisar sin tener que entrar en la pantalla.
/// </summary>
/// <remarks>
/// Sólo recuentos: el menú lo pide en cada navegación, y traer los movimientos con su
/// contenido original para contarlos en el navegador sería mover miles de filas para
/// pintar un número.
/// </remarks>
public sealed record PendingReviewResponse(int Transfers, int Transactions, int ManualDuplicates = 0)
{
    public int Total => Transfers + Transactions + ManualDuplicates;
}

/// <summary>
/// Un apunte manual y un importado que parecen el mismo movimiento.
/// </summary>
/// <remarks>
/// Un manual no tiene huella de origen, así que la deduplicación no lo reconoce. La pareja
/// se enseña para que decida una persona: borrar el manual o marcar que son distintos.
/// </remarks>
public sealed record ManualDuplicateResponse(TransactionResponse Manual, TransactionResponse Imported);

/// <summary>Una fila de una importación pendiente que coincide con un apunte manual de la cuenta.</summary>
public sealed record ManualMatchResponse(
    int? RowNumber,
    DateTimeOffset OccurredAt,
    string Type,
    string? AssetSymbol,
    decimal Quantity,
    Guid ManualId,
    DateTimeOffset ManualOccurredAt,
    string? ManualNote);

/// <summary>Qué ejercicio puede cambiar una corrección y si es anterior al actual.</summary>
public sealed record CorrectionImpactResponse(int TaxYear, bool IsPastYear);

/// <summary>Traspaso propuesto, pendiente de que el usuario confirme o rechace.</summary>
public sealed record InternalTransferResponse(
    Guid Id,
    Guid AssetId,
    string AssetSymbol,
    Guid SourceAccountId,
    Guid DestinationAccountId,
    decimal SentQuantity,
    decimal ReceivedQuantity,
    decimal NetworkFeeQuantity,
    string Status,
    DateTimeOffset ProposedAt);

/// <summary>Resultado de subir un fichero: lo que se importaría si se confirma.</summary>
public sealed record ImportPreviewResponse(ImportRunResponse Run, IReadOnlyList<string> Warnings);

/// <summary>Qué ha pasado al releer los movimientos sin clasificar.</summary>
/// <param name="NotSupported">Los que no se pueden releer, porque su origen no conserva forma de hacerlo.</param>
public sealed record ReinterpretationResponse(int Reclassified, int StillUnknown, int NotSupported);

/// <summary>Qué ha dado una sincronización lanzada a mano.</summary>
public sealed record SynchronizationResponse(
    int Accounts,
    int Imported,
    int Failed,
    int NewRecords,
    IReadOnlyList<string> Problems);

/// <summary>
/// Una página de movimientos con lo que hace falta para navegarlos.
/// </summary>
/// <remarks>
/// Van por páginas porque un histórico de cripto son miles de apuntes: dos mil de ellos
/// pueden ser recompensas diarias de un mismo producto, y traerlos todos para enseñar
/// veinte deja la pantalla en blanco mientras llegan.
/// </remarks>
public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<string> Types,
    IReadOnlyList<int> Years);


/// <summary>Un día de la evolución de la cartera.</summary>
/// <param name="ContributionInEuros">Aportado menos retirado ese día, que no es rendimiento.</param>
/// <param name="IsComplete">Falso cuando falta el precio de algún activo con posición.</param>
public sealed record PortfolioHistoryDayResponse(
    DateOnly Date,
    decimal ValueInEuros,
    decimal ContributionInEuros,
    bool IsComplete);

/// <summary>La evolución de la cartera en un periodo.</summary>
/// <param name="IncompleteDays">Cuántos días les falta algún precio.</param>
public sealed record PortfolioHistoryResponse(
    IReadOnlyList<PortfolioHistoryDayResponse> Days,
    IReadOnlyList<ClassHistoryDayResponse> ByClass,
    int IncompleteDays);

/// <summary>El valor de cada clase de activo un día.</summary>
public sealed record ClassHistoryDayResponse(DateOnly Date, IReadOnlyDictionary<string, decimal> ValueByClass);

/// <summary>Un día de la evolución de un activo. Sin precio, el valor viaja vacío.</summary>
public sealed record AssetHistoryDayResponse(
    DateOnly Date,
    decimal Quantity,
    decimal? PriceInEuros,
    decimal? ValueInEuros);

/// <summary>La evolución de un activo con sus indicadores.</summary>
public sealed record AssetHistoryResponse(
    Guid AssetId,
    string AssetSymbol,
    IReadOnlyList<AssetHistoryDayResponse> Days,
    IReadOnlyList<IndicatorPointResponse> SimpleMovingAverage,
    IReadOnlyList<IndicatorPointResponse> ExponentialMovingAverage,
    IReadOnlyList<IndicatorPointResponse> RelativeStrengthIndex,
    int IndicatorWindowDays);

/// <summary>Un valor de un indicador con el día al que corresponde.</summary>
public sealed record IndicatorPointResponse(DateOnly Date, decimal Value);

/// <summary>Rendimiento y riesgo de un periodo, con su referencia.</summary>
/// <param name="TimeWeightedReturn">Rentabilidad que juzga las decisiones, en tanto por uno.</param>
/// <param name="MoneyWeightedReturn">Rentabilidad que juzga el resultado. Vacía si no se puede resolver.</param>
/// <param name="BenchmarkSymbol">Activo tomado como referencia, o vacío si no hay ninguno.</param>
/// <param name="BenchmarkReturn">Lo que habría rendido la referencia con las mismas aportaciones.</param>
public sealed record PerformanceResponse(
    DateOnly From,
    DateOnly To,
    decimal TimeWeightedReturn,
    decimal? MoneyWeightedReturn,
    decimal Volatility,
    decimal MaximumDrawdown,
    int? DrawdownRecoveredInDays,
    decimal ContributedInEuros,
    decimal ValueInEuros,
    string? BenchmarkSymbol,
    decimal? BenchmarkReturn,
    decimal? BenchmarkValueInEuros,
    bool IsComplete,
    bool BenchmarkIsComplete);

/// <summary>
/// Un movimiento tal como se escribe en el formulario: para apuntarlo a mano, editarlo o
/// corregir un importado.
/// </summary>
/// <param name="AssetSymbol">Símbolo del activo, o nulo en un movimiento sólo de dinero.</param>
/// <param name="AssetClass">Clase del activo, para darlo de alta si aún no existe: <c>Crypto</c> o <c>Equity</c>.</param>
/// <param name="TimeZoneId">Zona horaria en la que ocurrió, para saber a qué día pertenece.</param>
/// <param name="Text">Nota de un movimiento manual o motivo de una corrección.</param>
public sealed record ManualMovementRequest(
    Guid AccountId,
    string Type,
    string? AssetSymbol,
    string? AssetClass,
    decimal Quantity,
    decimal? UnitPrice,
    decimal GrossAmount,
    string Currency,
    decimal Fee,
    DateTimeOffset OccurredAt,
    string TimeZoneId,
    string? Text);

/// <summary>Motivo de una anulación.</summary>
public sealed record VoidMovementRequest(string Reason);
