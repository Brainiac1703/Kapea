using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Strategies;

/// <summary>Cómo se fija un precio a partir de otro.</summary>
public enum LevelKind
{
    /// <summary>Un porcentaje sobre el precio de entrada.</summary>
    Percentage = 1,

    /// <summary>Un múltiplo de lo que el activo se mueve en un día.</summary>
    RangeMultiple = 2,

    /// <summary>Un múltiplo de la distancia hasta el nivel de salida.</summary>
    RiskMultiple = 3,
}

/// <summary>
/// Cómo se calcula el objetivo o el nivel de salida de una entrada.
/// </summary>
/// <param name="Factor">Porcentaje o múltiplo, según la clase.</param>
public sealed record Level(LevelKind Kind, decimal Factor)
{
    public void Ensure()
    {
        if (Factor <= 0m)
        {
            throw new DomainException("El factor de un nivel tiene que ser mayor que cero.");
        }
    }
}

/// <summary>
/// Una versión de un sistema: las reglas tal como estaban en un momento dado.
/// </summary>
/// <remarks>
/// Es inmutable. Corregir un sistema crea otra versión y conserva esta, porque una señal
/// ya emitida tiene que seguir diciendo con qué reglas salió.
/// </remarks>
public sealed class StrategyVersion
{
    private StrategyVersion()
    {
        Entry = null!;
    }

    private StrategyVersion(int number, DateTimeOffset createdAt, Condition entry)
    {
        Number = number;
        CreatedAt = createdAt;
        Entry = entry;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public int Number { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Cuándo se entra.</summary>
    public Condition Entry { get; private set; }

    /// <summary>Cuándo se sale, si el sistema lo declara aparte de su objetivo.</summary>
    public Condition? Exit { get; private set; }

    /// <summary>Cómo se fija el objetivo, si lo hay.</summary>
    public Level? Target { get; private set; }

    /// <summary>Cómo se fija el nivel de salida, si lo hay.</summary>
    public Level? StopLoss { get; private set; }

    /// <summary>
    /// Días de histórico que hacen falta antes de poder evaluar el sistema.
    /// </summary>
    /// <remarks>
    /// Es el mayor de sus condiciones: con menos días, la ventana de algún indicador
    /// estaría a medias y la señal diría lo que dijera el trozo que hay.
    /// </remarks>
    public int RequiredDays => Math.Max(Entry.RequiredDays, Exit?.RequiredDays ?? 0);

    public static StrategyVersion Create(
        int number,
        DateTimeOffset createdAt,
        Condition entry,
        Condition? exit = null,
        Level? target = null,
        Level? stopLoss = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        entry.Ensure();
        exit?.Ensure();
        target?.Ensure();
        stopLoss?.Ensure();

        // Un objetivo medido en múltiplos del riesgo no significa nada sin un nivel de
        // salida del que medir ese riesgo.
        if (target?.Kind == LevelKind.RiskMultiple && stopLoss is null)
        {
            throw new DomainException(
                "Un objetivo en múltiplos del riesgo necesita un nivel de salida del que medirlo.");
        }

        if (exit is null && target is null && stopLoss is null)
        {
            throw new DomainException(
                "Un sistema sin salida, sin objetivo y sin nivel de salida nunca cerraría una posición.");
        }

        return new StrategyVersion(number, createdAt, entry)
        {
            Exit = exit,
            Target = target,
            StopLoss = stopLoss,
        };
    }
}

/// <summary>
/// Un sistema de especulación: un conjunto de reglas con nombre, guardado como dato.
/// </summary>
/// <remarks>
/// Va versionado por lo mismo que los perfiles de importación: las reglas se corrigen con
/// el uso, y lo ya emitido con las anteriores tiene que seguir explicándose.
/// </remarks>
public sealed class Strategy
{
    private readonly List<StrategyVersion> _versions = [];

    private Strategy()
    {
        Name = null!;
    }

    private Strategy(Guid id, UserId userId, string name, string? description)
    {
        Id = id;
        UserId = userId;
        Name = name;
        Description = description;
    }

    public Guid Id { get; private set; }

    public UserId UserId { get; private set; }

    public string Name { get; private set; }

    /// <summary>De dónde sale el sistema, para poder recordarlo meses después.</summary>
    public string? Description { get; private set; }

    public IReadOnlyList<StrategyVersion> Versions => _versions;

    public int CurrentVersion => _versions.Count == 0 ? 0 : _versions.Max(version => version.Number);

    public StrategyVersion Current =>
        _versions.SingleOrDefault(version => version.Number == CurrentVersion)
        ?? throw new DomainException("El sistema no tiene ninguna versión.");

    public static Strategy Create(
        UserId userId,
        string name,
        Func<int, StrategyVersion> version,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(version);

        var strategy = new Strategy(Guid.NewGuid(), userId, name.Trim(), Clean(description));

        strategy._versions.Add(version(1));

        return strategy;
    }

    /// <summary>Guarda una versión nueva y conserva las anteriores.</summary>
    public StrategyVersion Revise(Func<int, StrategyVersion> version, string? name = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (description is not null)
        {
            Description = Clean(description);
        }

        var next = version(CurrentVersion + 1);

        _versions.Add(next);

        return next;
    }

    public StrategyVersion? FindVersion(int number) =>
        _versions.SingleOrDefault(version => version.Number == number);

    private static string? Clean(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
