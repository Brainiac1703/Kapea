using Kapea.Shared.Contracts;

namespace Kapea.Client.Models;

/// <summary>
/// Una condición mientras se edita.
/// </summary>
/// <remarks>
/// La pantalla trabaja con una lista plana de comparaciones unidas por «todas» o
/// «cualquiera». El contrato admite árboles anidados, pero mezclarlos en un formulario
/// exige un editor de árbol que no aporta nada mientras ningún sistema real lo pida.
/// </remarks>
public sealed class ComparisonModel
{
    public string LeftOperand { get; set; } = "Price";

    public int LeftWindow { get; set; } = 50;

    public decimal LeftValue { get; set; }

    public string Comparison { get; set; } = "CrossesAbove";

    public string RightOperand { get; set; } = "SimpleMovingAverage";

    public int RightWindow { get; set; } = 200;

    public decimal RightValue { get; set; }

    public ConditionResponse ToRequest() =>
        new(
            "Comparison",
            Term(LeftOperand, LeftWindow, LeftValue),
            Comparison,
            Term(RightOperand, RightWindow, RightValue),
            null);

    public static ComparisonModel From(ConditionResponse condition) => new()
    {
        LeftOperand = condition.Left?.Operand ?? "Price",
        LeftWindow = condition.Left?.Window ?? 50,
        LeftValue = condition.Left?.Value ?? 0m,
        Comparison = condition.Comparison ?? "GreaterThan",
        RightOperand = condition.Right?.Operand ?? "SimpleMovingAverage",
        RightWindow = condition.Right?.Window ?? 200,
        RightValue = condition.Right?.Value ?? 0m,
    };

    private static TermResponse Term(string operand, int window, decimal value) => operand switch
    {
        "Constant" => new TermResponse(operand, null, value),
        "Price" => new TermResponse(operand, null, null),
        _ => new TermResponse(operand, window, null),
    };
}

/// <summary>Un nivel mientras se edita. Sin clase, no se declara.</summary>
public sealed class LevelModel
{
    public string Kind { get; set; } = string.Empty;

    public decimal Factor { get; set; } = 2m;

    public LevelResponse? ToRequest() =>
        string.IsNullOrWhiteSpace(Kind) ? null : new LevelResponse(Kind, Factor);

    public static LevelModel From(LevelResponse? level) => new()
    {
        Kind = level?.Kind ?? string.Empty,
        Factor = level?.Factor ?? 2m,
    };
}

/// <summary>Un sistema mientras se edita.</summary>
public sealed class StrategyModel
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>«All» o «Any»: si tienen que cumplirse todas las condiciones o cualquiera.</summary>
    public string Junction { get; set; } = "All";

    public List<ComparisonModel> Entry { get; set; } = [new()];

    /// <summary>La salida es opcional y de una sola condición.</summary>
    public bool HasExit { get; set; }

    public ComparisonModel Exit { get; set; } = new();

    public LevelModel Target { get; set; } = new();

    public LevelModel StopLoss { get; set; } = new();

    /// <summary>El error de lo último que se intentó guardar, para no cerrar el formulario con él.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Lo que una regla puede nombrar y con qué traducir una descripción.
    /// </summary>
    /// <remarks>
    /// Viajan en el modelo y no como parámetros del diálogo porque el diálogo solo recibe
    /// su contenido, igual que ocurre con las cuentas en el alta de una credencial.
    /// </remarks>
    public StrategyVocabularyResponse? Vocabulary { get; set; }

    /// <summary>Con qué traducir una descripción. Nulo cuando no hay servicio.</summary>
    public Func<string, Task<StrategyProposalResponse>>? Translate { get; set; }

    /// <summary>El método descrito con palabras, para que el traductor lo convierta en reglas.</summary>
    public string Description2 { get; set; } = string.Empty;

    /// <summary>Lo que el traductor no ha sabido traducir, para enseñarlo sin ocultarlo.</summary>
    public IReadOnlyList<string> NotUnderstood { get; set; } = [];

    /// <summary>Seguridad declarada por el traductor de la última propuesta.</summary>
    public double? Confidence { get; set; }

    /// <summary>Sustituye las reglas por las propuestas, conservando lo demás.</summary>
    public void Apply(StrategyProposalResponse proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        NotUnderstood = proposal.NotUnderstood;
        Confidence = proposal.Confidence;

        if (proposal.Rules is not { } rules)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Name) && proposal.Name is { Length: > 0 } name)
        {
            Name = name;
        }

        Junction = rules.Entry.Junction == "Comparison" ? "All" : rules.Entry.Junction;
        Entry = rules.Entry.Junction == "Comparison"
            ? [ComparisonModel.From(rules.Entry)]
            : [.. (rules.Entry.Children ?? []).Select(ComparisonModel.From)];
        HasExit = rules.Exit is not null;
        Exit = rules.Exit is null ? new ComparisonModel() : ComparisonModel.From(rules.Exit);
        Target = LevelModel.From(rules.Target);
        StopLoss = LevelModel.From(rules.StopLoss);
    }

    public StrategyRulesRequest ToRules() =>
        new(
            Entry.Count == 1
                ? Entry[0].ToRequest()
                : new ConditionResponse(Junction, null, null, null, [.. Entry.Select(entry => entry.ToRequest())]),
            HasExit ? Exit.ToRequest() : null,
            Target.ToRequest(),
            StopLoss.ToRequest());

    public static StrategyModel From(StrategyResponse strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        var version = strategy.Versions.Single(entry => entry.Number == strategy.CurrentVersion);

        return new StrategyModel
        {
            Id = strategy.Id,
            Name = strategy.Name,
            Description = strategy.Description ?? string.Empty,
            Junction = version.Entry.Junction == "Comparison" ? "All" : version.Entry.Junction,
            Entry = version.Entry.Junction == "Comparison"
                ? [ComparisonModel.From(version.Entry)]
                : [.. (version.Entry.Children ?? []).Select(ComparisonModel.From)],
            HasExit = version.Exit is not null,
            Exit = version.Exit is null ? new ComparisonModel() : ComparisonModel.From(version.Exit),
            Target = LevelModel.From(version.Target),
            StopLoss = LevelModel.From(version.StopLoss),
        };
    }
}
