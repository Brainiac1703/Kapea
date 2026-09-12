using Kapea.Domain.Strategies;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class StrategyPersistenceTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_saved_version_comes_back_with_the_same_rules()
    {
        var user = new UserId(Guid.NewGuid());

        var entry = Condition.All(
            Condition.When(Term.Close, Comparison.CrossesAbove, Term.Indicator(Operand.SimpleMovingAverage, 50)),
            Condition.When(Term.Close, Comparison.GreaterThan, Term.Indicator(Operand.SimpleMovingAverage, 200)));

        var strategy = Strategy.Create(
            user,
            "Cruce con filtro de tendencia",
            number => StrategyVersion.Create(
                number,
                Now,
                entry,
                stopLoss: new Level(LevelKind.RangeMultiple, 2m),
                target: new Level(LevelKind.RiskMultiple, 3m)),
            "Entra cuando cruza su media de cincuenta estando por encima de la de doscientos.");

        await using (var context = fixture.CreateContext(user))
        {
            context.Strategies.Add(strategy);
            await context.SaveChangesAsync();
        }

        await using var reading = fixture.CreateContext(user);
        var stored = await reading.Strategies.SingleAsync(entity => entity.Id == strategy.Id);

        Assert.Equal(1, stored.CurrentVersion);
        Assert.Equal(entry.Describe(), stored.Current.Entry.Describe());
        Assert.Equal(LevelKind.RangeMultiple, stored.Current.StopLoss!.Kind);
        Assert.Equal(3m, stored.Current.Target!.Factor);
        Assert.Equal(200, stored.Current.RequiredDays);
    }

    [Fact]
    public async Task Correcting_a_strategy_keeps_the_previous_version_reachable()
    {
        var user = new UserId(Guid.NewGuid());
        var strategy = Strategy.Create(user, "Con versiones", number => Version(number, 50));

        await using (var context = fixture.CreateContext(user))
        {
            context.Strategies.Add(strategy);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext(user))
        {
            var stored = await context.Strategies.SingleAsync(entity => entity.Id == strategy.Id);

            stored.Revise(number => Version(number, 100));

            await context.SaveChangesAsync();
        }

        await using var reading = fixture.CreateContext(user);
        var revised = await reading.Strategies.SingleAsync(entity => entity.Id == strategy.Id);

        Assert.Equal(2, revised.CurrentVersion);
        Assert.Equal(100, revised.Current.Entry.RequiredDays);

        // La anterior sigue ahí: una señal ya emitida tiene que poder explicarse.
        Assert.Equal(50, revised.FindVersion(1)!.Entry.RequiredDays);
    }

    [Fact]
    public async Task A_strategy_of_another_person_is_not_visible()
    {
        var mine = new UserId(Guid.NewGuid());
        var theirs = new UserId(Guid.NewGuid());

        await using (var context = fixture.CreateContext(theirs))
        {
            context.Strategies.Add(Strategy.Create(theirs, "Ajena", number => Version(number, 50)));
            await context.SaveChangesAsync();
        }

        await using var reading = fixture.CreateContext(mine);

        Assert.Empty(await reading.Strategies.ToListAsync());
    }

    [Fact]
    public async Task The_same_signal_is_not_stored_twice()
    {
        // El motor vuelve a producir las señales antiguas cada vez que hay precios
        // nuevos. Sin la huella, cada vuelta duplicaría el histórico.
        var user = new UserId(Guid.NewGuid());
        var strategy = Guid.NewGuid();
        var asset = Guid.NewGuid();

        var signal = new Signal(
            asset, new DateOnly(2026, 3, 10), SignalDirection.Entry, Money.Euros(100m), "porque sí");

        await using var context = fixture.CreateContext(user);

        context.EmittedSignals.Add(EmittedSignal.From(user, strategy, 1, signal));
        await context.SaveChangesAsync();

        context.EmittedSignals.Add(EmittedSignal.From(user, strategy, 1, signal));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_signal_keeps_its_levels_and_its_version()
    {
        var user = new UserId(Guid.NewGuid());

        var signal = new Signal(
            Guid.NewGuid(),
            new DateOnly(2026, 3, 10),
            SignalDirection.Entry,
            Money.Euros(100m),
            "el precio ha cruzado su media",
            Money.Euros(120m),
            Money.Euros(90m));

        await using (var context = fixture.CreateContext(user))
        {
            context.EmittedSignals.Add(EmittedSignal.From(user, Guid.NewGuid(), 3, signal));
            await context.SaveChangesAsync();
        }

        await using var reading = fixture.CreateContext(user);
        var stored = await reading.EmittedSignals.SingleAsync();

        Assert.Equal(3, stored.StrategyVersion);
        Assert.Equal(Money.Euros(120m), stored.Target);
        Assert.Equal(Money.Euros(90m), stored.StopLoss);
    }

    private static StrategyVersion Version(int number, int window) =>
        StrategyVersion.Create(
            number,
            Now,
            Condition.When(Term.Close, Comparison.CrossesAbove, Term.Indicator(Operand.SimpleMovingAverage, window)),
            stopLoss: new Level(LevelKind.Percentage, 0.08m));
}
