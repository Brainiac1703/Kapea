using Kapea.Domain.Common;
using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Strategies;

public class ConditionTests
{
    [Fact]
    public void A_comparison_needs_both_sides()
    {
        var broken = new Condition(Junction.Comparison, Term.Close, Comparison.GreaterThan, null);

        Assert.Throws<DomainException>(broken.Ensure);
    }

    [Fact]
    public void An_indicator_without_a_window_is_rejected()
    {
        // Una media sin días no es una media, y descubrirlo en medio de una simulación
        // deja media cartera calculada y la otra mitad no.
        var broken = Condition.When(
            new Term(Operand.SimpleMovingAverage), Comparison.GreaterThan, Term.Of(100m));

        Assert.Throws<DomainException>(broken.Ensure);
    }

    [Fact]
    public void A_constant_without_a_value_is_rejected()
    {
        var broken = Condition.When(Term.Close, Comparison.GreaterThan, new Term(Operand.Constant));

        Assert.Throws<DomainException>(broken.Ensure);
    }

    [Fact]
    public void A_combination_without_children_is_rejected() =>
        Assert.Throws<DomainException>(Condition.All().Ensure);

    [Fact]
    public void A_well_formed_condition_is_accepted() =>
        Condition.All(
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Indicator(Operand.SimpleMovingAverage, 200)),
            Condition.When(Term.Indicator(Operand.RelativeStrengthIndex, 14), Comparison.LessThan, Term.Of(30m)))
            .Ensure();

    [Fact]
    public void The_required_days_are_those_of_its_widest_window()
    {
        var condition = Condition.All(
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Indicator(Operand.SimpleMovingAverage, 200)),
            Condition.When(Term.Indicator(Operand.RelativeStrengthIndex, 14), Comparison.LessThan, Term.Of(30m)));

        Assert.Equal(200, condition.RequiredDays);
    }

    [Fact]
    public void A_condition_reads_in_words()
    {
        var condition = Condition.When(
            Term.Close, Comparison.CrossesAbove, Term.Indicator(Operand.SimpleMovingAverage, 50));

        // El nombre del enumerado no vale: la explicación de una señal es justamente lo
        // que la hace útil, y «SimpleMovingAverage de 50 días» no se lee.
        Assert.Equal("el precio cruza al alza su media móvil simple de 50 días", condition.Describe());
    }
}

public class StrategyTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_strategy_starts_with_its_first_version()
    {
        var strategy = Strategy.Create(Owner, "Cruce de medias", number => Version(number));

        Assert.Equal(1, strategy.CurrentVersion);
        Assert.Single(strategy.Versions);
    }

    [Fact]
    public void Correcting_it_keeps_the_previous_version()
    {
        // Una señal ya emitida tiene que seguir diciendo con qué reglas salió.
        var strategy = Strategy.Create(Owner, "Cruce de medias", number => Version(number));

        strategy.Revise(number => Version(number, window: 100));

        Assert.Equal(2, strategy.CurrentVersion);
        Assert.Equal(2, strategy.Versions.Count);
        Assert.NotNull(strategy.FindVersion(1));
    }

    [Fact]
    public void A_strategy_that_never_closes_a_position_is_rejected()
    {
        // Sin salida, sin objetivo y sin nivel de salida, una entrada se queda abierta
        // para siempre y la simulación no diría nada.
        Assert.Throws<DomainException>(() => StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m))));
    }

    [Fact]
    public void A_target_measured_in_risk_needs_a_stop_to_measure_it_from()
    {
        Assert.Throws<DomainException>(() => StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            target: new Level(LevelKind.RiskMultiple, 2m)));
    }

    [Fact]
    public void A_level_with_a_factor_of_zero_is_rejected() =>
        Assert.Throws<DomainException>(() => StrategyVersion.Create(
            1,
            Now,
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Of(100m)),
            stopLoss: new Level(LevelKind.Percentage, 0m)));

    [Fact]
    public void The_required_days_come_from_its_widest_condition()
    {
        var version = Version(1, window: 200);

        Assert.Equal(200, version.RequiredDays);
    }

    private static StrategyVersion Version(int number, int window = 50) =>
        StrategyVersion.Create(
            number,
            Now,
            Condition.When(Term.Close, Comparison.CrossesAbove, Term.Indicator(Operand.SimpleMovingAverage, window)),
            stopLoss: new Level(LevelKind.Percentage, 0.08m),
            target: new Level(LevelKind.RiskMultiple, 2m));
}
