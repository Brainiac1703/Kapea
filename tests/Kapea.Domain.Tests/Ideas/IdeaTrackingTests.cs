using Kapea.Domain.Common;
using Kapea.Domain.Ideas;
using Kapea.Domain.MarketData;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Ideas;

public class ExternalIdeaTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Source = Guid.NewGuid();

    [Fact]
    public void An_idea_without_levels_is_still_worth_keeping()
    {
        // Calcularle unos niveles sería inventarlos: la fuente no los dio.
        var idea = ExternalIdea.Create(Owner, Source, "san.es", IdeaDirection.Buy, new DateOnly(2026, 3, 1));

        Assert.Equal("SAN.ES", idea.Symbol);
        Assert.Null(idea.Target);
        Assert.False(idea.CanBeTracked);
    }

    [Fact]
    public void A_buy_with_its_target_below_its_exit_level_is_rejected() =>
        Assert.Throws<DomainException>(() => ExternalIdea.Create(
            Owner, Source, "BTC", IdeaDirection.Buy, new DateOnly(2026, 3, 1),
            target: Money.Euros(90m), stopLoss: Money.Euros(100m)));

    [Fact]
    public void A_sale_has_its_levels_the_other_way_round()
    {
        // En una venta el objetivo va por debajo y la salida por encima. Comprobarlo
        // como una compra rechazaría ideas correctas.
        var idea = ExternalIdea.Create(
            Owner, Source, "BTC", IdeaDirection.Sell, new DateOnly(2026, 3, 1),
            target: Money.Euros(80m), stopLoss: Money.Euros(110m));

        Assert.Equal(IdeaDirection.Sell, idea.Direction);

        Assert.Throws<DomainException>(() => ExternalIdea.Create(
            Owner, Source, "BTC", IdeaDirection.Sell, new DateOnly(2026, 3, 1),
            target: Money.Euros(110m), stopLoss: Money.Euros(80m)));
    }

    [Fact]
    public void An_idea_can_only_be_resolved_once()
    {
        var idea = Idea();

        idea.Resolve(IdeaOutcome.Reached, new DateOnly(2026, 3, 10));
        idea.Resolve(IdeaOutcome.Stopped, new DateOnly(2026, 3, 20));

        Assert.Equal(IdeaOutcome.Reached, idea.Outcome);
        Assert.Equal(new DateOnly(2026, 3, 10), idea.ResolvedOn);
    }

    [Fact]
    public void A_source_only_moves_its_mark_forward()
    {
        var source = IdeaSource.Create(Owner, "Un analista", "canal");

        source.Seen(new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero), "https://ejemplo/nuevo");
        source.Seen(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), "https://ejemplo/viejo");

        Assert.Equal("https://ejemplo/nuevo", source.LastSeenUrl);
    }

    private static ExternalIdea Idea() => ExternalIdea.Create(
        Owner, Source, "BTC", IdeaDirection.Buy, new DateOnly(2026, 3, 1),
        assetId: Guid.NewGuid(), entry: Money.Euros(100m),
        target: Money.Euros(120m), stopLoss: Money.Euros(90m));
}

public class IdeaTrackingTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Source = Guid.NewGuid();
    private static readonly Guid Asset = Guid.NewGuid();

    [Fact]
    public void An_idea_that_reaches_its_target_is_resolved_as_reached()
    {
        var resolution = IdeaTracking.Resolve(Idea(), Prices(100m, 110m, 125m), new DateOnly(2026, 3, 10));

        Assert.Equal(IdeaOutcome.Reached, resolution.Outcome);
        Assert.Equal(new DateOnly(2026, 3, 3), resolution.On);
    }

    [Fact]
    public void An_idea_that_hits_its_exit_level_is_resolved_as_stopped()
    {
        var resolution = IdeaTracking.Resolve(Idea(), Prices(100m, 95m, 85m), new DateOnly(2026, 3, 10));

        Assert.Equal(IdeaOutcome.Stopped, resolution.Outcome);
        Assert.Equal(new DateOnly(2026, 3, 3), resolution.On);
    }

    [Fact]
    public void An_idea_that_reached_neither_level_is_still_alive()
    {
        var resolution = IdeaTracking.Resolve(Idea(), Prices(100m, 105m, 110m), new DateOnly(2026, 3, 10));

        Assert.Equal(IdeaOutcome.Open, resolution.Outcome);
        Assert.Null(resolution.On);
    }

    [Fact]
    public void An_idea_past_its_term_expires()
    {
        var resolution = IdeaTracking.Resolve(
            Idea(), Prices(100m, 105m, 110m), new DateOnly(2026, 9, 1), expiryDays: 30);

        Assert.Equal(IdeaOutcome.Expired, resolution.Outcome);
        Assert.Equal(new DateOnly(2026, 3, 31), resolution.On);
    }

    [Fact]
    public void When_a_day_touches_both_levels_the_exit_level_wins()
    {
        // Con un cierre diario no se sabe qué se tocó primero, igual que en el simulador.
        var idea = ExternalIdea.Create(
            Owner, Source, "BTC", IdeaDirection.Buy, new DateOnly(2026, 3, 1),
            assetId: Asset, entry: Money.Euros(100m),
            target: Money.Euros(101m), stopLoss: Money.Euros(99m));

        var resolution = IdeaTracking.Resolve(idea, Prices(100m, 50m), new DateOnly(2026, 3, 10));

        Assert.Equal(IdeaOutcome.Stopped, resolution.Outcome);
    }

    [Fact]
    public void A_sale_is_tracked_the_other_way_round()
    {
        var idea = ExternalIdea.Create(
            Owner, Source, "BTC", IdeaDirection.Sell, new DateOnly(2026, 3, 1),
            assetId: Asset, entry: Money.Euros(100m),
            target: Money.Euros(80m), stopLoss: Money.Euros(110m));

        Assert.Equal(
            IdeaOutcome.Reached,
            IdeaTracking.Resolve(idea, Prices(100m, 90m, 75m), new DateOnly(2026, 3, 10)).Outcome);
    }

    [Fact]
    public void An_idea_that_cannot_be_tracked_stays_open()
    {
        var idea = ExternalIdea.Create(Owner, Source, "BTC", IdeaDirection.Buy, new DateOnly(2026, 3, 1));

        Assert.Equal(
            IdeaOutcome.Open,
            IdeaTracking.Resolve(idea, Prices(100m, 200m), new DateOnly(2026, 3, 10)).Outcome);
    }

    [Fact]
    public void Prices_before_the_publication_are_ignored()
    {
        // Lo que el precio hiciera antes de la idea no la resuelve.
        var series = new List<DailyPrice>
        {
            new(Asset, new DateOnly(2026, 2, 1), 200m, "Prueba"),
            new(Asset, new DateOnly(2026, 3, 1), 100m, "Prueba"),
        };

        Assert.Equal(
            IdeaOutcome.Open, IdeaTracking.Resolve(Idea(), series, new DateOnly(2026, 3, 5)).Outcome);
    }

    private static ExternalIdea Idea() => ExternalIdea.Create(
        Owner, Source, "BTC", IdeaDirection.Buy, new DateOnly(2026, 3, 1),
        assetId: Asset, entry: Money.Euros(100m),
        target: Money.Euros(120m), stopLoss: Money.Euros(90m));

    private static List<DailyPrice> Prices(params decimal[] prices) =>
        [.. prices.Select((price, index) => new DailyPrice(Asset, new DateOnly(2026, 3, 1).AddDays(index), price, "Prueba"))];
}

public class SourceBalanceTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Source = Guid.NewGuid();

    [Fact]
    public void A_source_with_nothing_resolved_says_so_instead_of_zero()
    {
        var balance = SourceBalanceCalculator.Of(Source, "Un analista", [Idea(IdeaOutcome.Open)], 0.0095m);

        Assert.Null(balance.ReturnNetOfFees);
        Assert.Equal(1, balance.Open);
    }

    [Fact]
    public void The_balance_counts_each_outcome()
    {
        var balance = SourceBalanceCalculator.Of(
            Source,
            "Un analista",
            [Idea(IdeaOutcome.Reached), Idea(IdeaOutcome.Reached), Idea(IdeaOutcome.Stopped), Idea(IdeaOutcome.Expired)],
            0m);

        Assert.Equal(2, balance.Reached);
        Assert.Equal(1, balance.Stopped);
        Assert.Equal(1, balance.Expired);
    }

    [Fact]
    public void The_return_is_net_of_the_commission_of_both_sides()
    {
        // Objetivo a un veinte por ciento, con un 0,95 % por lado: queda un 18,1 %.
        var balance = SourceBalanceCalculator.Of(Source, "Un analista", [Idea(IdeaOutcome.Reached)], 0.0095m);

        Assert.Equal(0.181m, decimal.Round(balance.ReturnNetOfFees!.Value, 4));
    }

    [Fact]
    public void A_source_that_hits_two_of_three_can_still_lose_money()
    {
        // Dos aciertos de un dos por ciento y un fallo de un diez no cubren las
        // comisiones: una fuente que acierta más de lo que falla puede salir perdiendo.
        var ideas = new List<ExternalIdea>
        {
            Idea(IdeaOutcome.Reached, target: 102m, stop: 90m),
            Idea(IdeaOutcome.Reached, target: 102m, stop: 90m),
            Idea(IdeaOutcome.Stopped, target: 102m, stop: 90m),
        };

        var balance = SourceBalanceCalculator.Of(Source, "Un analista", ideas, 0.0095m);

        Assert.True(balance.ReturnNetOfFees < 0m);
    }

    private static ExternalIdea Idea(IdeaOutcome outcome, decimal target = 120m, decimal stop = 90m)
    {
        var idea = ExternalIdea.Create(
            Owner, Source, "BTC", IdeaDirection.Buy, new DateOnly(2026, 3, 1),
            assetId: Guid.NewGuid(), entry: Money.Euros(100m),
            target: Money.Euros(target), stopLoss: Money.Euros(stop));

        idea.Resolve(outcome, new DateOnly(2026, 3, 10));

        return idea;
    }
}
