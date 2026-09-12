using System.Text.Json.Serialization;

namespace Kapea.Shared.Contracts;

/// <summary>Un lado de una comparación, tal como viaja.</summary>
public sealed record TermResponse(string Operand, int? Window, decimal? Value);

/// <summary>Una condición, sola o combinada.</summary>
public sealed record ConditionResponse(
    string Junction,
    TermResponse? Left,
    string? Comparison,
    TermResponse? Right,
    IReadOnlyList<ConditionResponse>? Children)
{
    /// <summary>Cómo se lee en castellano, para enseñarla sin recomponerla en el cliente.</summary>
    public string? Description { get; init; }
}

/// <summary>Cómo se fija un precio a partir de otro.</summary>
public sealed record LevelResponse(string Kind, decimal Factor);

/// <summary>Una versión de un sistema.</summary>
public sealed record StrategyVersionResponse(
    int Number,
    DateTimeOffset CreatedAt,
    ConditionResponse Entry,
    ConditionResponse? Exit,
    LevelResponse? Target,
    LevelResponse? StopLoss,
    int RequiredDays);

/// <summary>Un sistema de especulación con sus versiones.</summary>
public sealed record StrategyResponse(
    Guid Id,
    string Name,
    string? Description,
    int CurrentVersion,
    IReadOnlyList<StrategyVersionResponse> Versions);

/// <summary>Las reglas que se envían al crear un sistema o al corregirlo.</summary>
public sealed record StrategyRulesRequest(
    ConditionResponse Entry,
    ConditionResponse? Exit,
    LevelResponse? Target,
    LevelResponse? StopLoss);

public sealed record CreateStrategyRequest(string Name, string? Description, StrategyRulesRequest Rules);

/// <summary>Corrección de un sistema. Guarda una versión nueva y conserva la anterior.</summary>
public sealed record ReviseStrategyRequest(string? Name, string? Description, StrategyRulesRequest Rules);

/// <summary>Una señal emitida.</summary>
public sealed record SignalResponse(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    int StrategyVersion,
    Guid AssetId,
    string AssetSymbol,
    DateOnly Date,
    string Direction,
    decimal PriceInEuros,
    string Reason,
    decimal? TargetInEuros,
    decimal? StopLossInEuros,
    int DaysOld);

/// <summary>Una operación simulada.</summary>
public sealed record SimulatedTradeResponse(
    DateOnly EntryDate,
    decimal EntryPrice,
    DateOnly? ExitDate,
    decimal? ExitPrice,
    decimal Quantity,
    decimal ResultInEuros,
    decimal FeesInEuros,
    string EntryReason,
    string? ExitReason);

/// <summary>Lo que deja una simulación.</summary>
/// <param name="BuyAndHoldInEuros">Lo que habría dado comprar el primer día y no tocar nada.</param>
public sealed record BacktestResponse(
    Guid StrategyId,
    string StrategyName,
    int StrategyVersion,
    Guid AssetId,
    string AssetSymbol,
    decimal CapitalInEuros,
    decimal ResultInEuros,
    decimal ResultAfterTaxInEuros,
    decimal FeesInEuros,
    decimal TaxInEuros,
    decimal BuyAndHoldInEuros,
    decimal MaximumDrawdown,
    int Trades,
    int Closed,
    int Winners,
    decimal FeeRate,
    IReadOnlyList<SimulatedTradeResponse> Detail)
{
    /// <summary>Si el sistema no bate a no hacer nada, el sistema sobra.</summary>
    [JsonIgnore]
    public bool BeatsBuyAndHold => ResultInEuros > BuyAndHoldInEuros;
}

/// <summary>Lo que dejó una pasada del motor de señales.</summary>
public sealed record SignalRunResponse(int Assets, int Emitted, int New);

/// <summary>Los campos que una regla puede usar, para poder construirla desde la pantalla.</summary>
public sealed record StrategyVocabularyResponse(
    IReadOnlyList<OperandResponse> Operands,
    IReadOnlyList<NamedValueResponse> Comparisons,
    IReadOnlyList<NamedValueResponse> Junctions,
    IReadOnlyList<NamedValueResponse> LevelKinds);

/// <param name="NeedsWindow">Si hay que declararle una ventana de días.</param>
public sealed record OperandResponse(string Operand, string Label, bool NeedsWindow);

public sealed record NamedValueResponse(string Value, string Label);

/// <summary>Un método descrito en palabras, para traducirlo a reglas.</summary>
public sealed record TranslateStrategyRequest(string Description);

/// <summary>
/// Lo que el traductor propone.
/// </summary>
/// <param name="Available">Falso cuando no hay servicio configurado.</param>
/// <param name="NotUnderstood">Lo que no se ha sabido traducir, dicho tal cual.</param>
/// <param name="Note">Por qué no hay propuesta, cuando no la hay.</param>
public sealed record StrategyProposalResponse(
    bool Available,
    string? Name,
    StrategyRulesRequest? Rules,
    IReadOnlyList<string> NotUnderstood,
    double Confidence,
    string? Note);

/// <summary>Qué dicen unas reglas, en castellano corriente.</summary>
public sealed record StrategyExplanationResponse(string? Explanation);

/// <summary>Una anotación del diario, con lo que acompaña.</summary>
/// <param name="Outcome">Qué pasó después, cuando se puede saber.</param>
public sealed record DecisionNoteResponse(
    Guid Id,
    Guid? TransactionId,
    Guid? SignalId,
    string Text,
    DateTimeOffset WrittenAt,
    string? About,
    string? Outcome);

/// <summary>Texto de una anotación, sobre un movimiento o sobre una señal.</summary>
public sealed record WriteNoteRequest(Guid? TransactionId, Guid? SignalId, string Text);

/// <summary>Una fuente externa que se sigue.</summary>
public sealed record IdeaSourceResponse(
    Guid Id,
    string Name,
    string? Channel,
    DateTimeOffset? LastSeenAt,
    string? LastSeenUrl,
    bool CanBeWatched);

public sealed record CreateIdeaSourceRequest(string Name, string? Channel);

/// <summary>Una idea con su desenlace.</summary>
public sealed record IdeaResponse(
    Guid Id,
    Guid SourceId,
    string SourceName,
    string Symbol,
    Guid? AssetId,
    string Direction,
    DateOnly PublishedOn,
    decimal? EntryInEuros,
    decimal? TargetInEuros,
    decimal? StopLossInEuros,
    string? Url,
    string? Note,
    string Outcome,
    DateOnly? ResolvedOn);

/// <summary>Una idea que se registra, ya revisada.</summary>
public sealed record CreateIdeaRequest(
    Guid SourceId,
    string Symbol,
    string Direction,
    DateOnly PublishedOn,
    decimal? Entry,
    decimal? Target,
    decimal? StopLoss,
    string? Url,
    string? Note);

/// <summary>Un texto pegado, para sacar ideas de él. El texto no se guarda.</summary>
public sealed record ExtractIdeasRequest(string Text);

/// <summary>Lo que se ha sacado del texto, para revisar antes de guardar.</summary>
public sealed record IdeaExtractionResponse(
    bool Available,
    IReadOnlyList<CreateIdeaRequest> Ideas,
    IReadOnlyList<string> NotUnderstood,
    string? Note);

/// <summary>Lo que ha dado una fuente.</summary>
public sealed record SourceBalanceResponse(
    Guid SourceId,
    string SourceName,
    int Reached,
    int Stopped,
    int Expired,
    int Open,
    decimal? ReturnNetOfFees);
