namespace Kapea.Shared.Contracts;

/// <summary>Cuenta del usuario en una plataforma.</summary>
public sealed record AccountResponse(Guid Id, string Platform, string Alias, string BaseCurrency);

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
    IReadOnlyList<RejectedRecordResponse> Rejected);

/// <summary>Registro rechazado con su contenido original y el motivo, para poder corregirlo.</summary>
public sealed record RejectedRecordResponse(Guid Id, int? RowNumber, string? NaturalId, string RawContent, string Reason);

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
    bool ExchangeRateWasSubstituted);

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
    DateTimeOffset? PriceAsOf);

/// <summary>Cartera completa, con la advertencia visible cuando las cifras están incompletas.</summary>
public sealed record PortfolioResponse(
    IReadOnlyList<OpenPositionResponse> Positions,
    decimal TotalCostInEuros,
    decimal? TotalMarketValueInEuros,
    int UnclassifiedTransactionCount,
    int PendingTransferCount,
    IReadOnlyList<string> Inconsistencies)
{
    /// <summary>Mientras haya movimientos sin resolver, estas cifras no están completas.</summary>
    public bool IsComplete => UnclassifiedTransactionCount == 0
        && PendingTransferCount == 0
        && Inconsistencies.Count == 0;
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
    IReadOnlyList<ConsumedLotResponse> ConsumedLots);

public sealed record ConsumedLotResponse(
    Guid LotId,
    Guid AcquisitionTransactionId,
    DateTimeOffset AcquiredAt,
    decimal Quantity,
    decimal AcquisitionCostInEuros,
    decimal ProceedsInEuros,
    decimal ResultInEuros);

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
