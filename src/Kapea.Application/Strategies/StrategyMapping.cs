using Kapea.Domain.Common;
using Kapea.Domain.Strategies;
using Kapea.Shared.Contracts;

namespace Kapea.Application.Strategies;

/// <summary>
/// Traduce entre las reglas del dominio y lo que viaja al cliente.
/// </summary>
/// <remarks>
/// El contrato lleva los nombres como texto y no como número: un enumerado serializado
/// por su valor convierte cualquier reordenación en un cambio silencioso de significado
/// de las reglas ya guardadas.
/// </remarks>
public static class StrategyMapping
{
    public static Condition ToDomain(ConditionResponse condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var junction = Parse<Junction>(condition.Junction, "combinación");

        if (junction == Junction.Comparison)
        {
            return new Condition(
                junction,
                ToDomain(condition.Left),
                Parse<Comparison>(condition.Comparison ?? string.Empty, "comparación"),
                ToDomain(condition.Right));
        }

        return new Condition(
            junction,
            Children: [.. (condition.Children ?? []).Select(ToDomain)]);
    }

    public static Level? ToDomain(LevelResponse? level) =>
        level is null ? null : new Level(Parse<LevelKind>(level.Kind, "clase de nivel"), level.Factor);

    public static StrategyVersion ToDomain(StrategyRulesRequest rules, int number, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return StrategyVersion.Create(
            number,
            createdAt,
            ToDomain(rules.Entry),
            rules.Exit is null ? null : ToDomain(rules.Exit),
            ToDomain(rules.Target),
            ToDomain(rules.StopLoss));
    }

    public static StrategyResponse ToResponse(Strategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        return new StrategyResponse(
            strategy.Id,
            strategy.Name,
            strategy.Description,
            strategy.CurrentVersion,
            [.. strategy.Versions.OrderBy(version => version.Number).Select(ToResponse)]);
    }

    public static StrategyVersionResponse ToResponse(StrategyVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return new StrategyVersionResponse(
            version.Number,
            version.CreatedAt,
            ToResponse(version.Entry),
            version.Exit is null ? null : ToResponse(version.Exit),
            ToResponse(version.Target),
            ToResponse(version.StopLoss),
            version.RequiredDays);
    }

    public static ConditionResponse ToResponse(Condition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return new ConditionResponse(
            condition.Junction.ToString(),
            ToResponse(condition.Left),
            condition.Comparison?.ToString(),
            ToResponse(condition.Right),
            condition.Children is null ? null : [.. condition.Children.Select(ToResponse)])
        {
            Description = condition.Describe(),
        };
    }

    private static LevelResponse? ToResponse(Level? level) =>
        level is null ? null : new LevelResponse(level.Kind.ToString(), level.Factor);

    private static TermResponse? ToResponse(Term? term) =>
        term is null ? null : new TermResponse(term.Operand.ToString(), term.Window, term.Value);

    private static Term? ToDomain(TermResponse? term) =>
        term is null ? null : new Term(Parse<Operand>(term.Operand, "operando"), term.Window, term.Value);

    private static TValue Parse<TValue>(string value, string what)
        where TValue : struct, Enum =>
        Enum.TryParse<TValue>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new DomainException($"«{value}» no es una {what} que Kapea sepa evaluar.");
}
