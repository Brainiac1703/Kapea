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
