using Kapea.Domain.Calculation;
using Kapea.Domain.Exchange;
using Kapea.Domain.MarketData;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Calculation;

public class PortfolioHistoryTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly Guid Bitcoin = Guid.NewGuid();
    private static readonly Guid Ether = Guid.NewGuid();

    [Fact]
    public void A_day_before_the_purchase_has_nothing_of_that_asset()
    {
        var days = Build(
            [Buy(Bitcoin, on: 5, quantity: 2m, cost: 100m)],
            Prices(Bitcoin, 60m, 60m, 60m, 60m, 60m, 60m, 60m));

        Assert.Empty(days[3].Assets);
        Assert.Equal(Money.Euros(0m), days[3].ValueInEuros);
    }

    [Fact]
    public void The_day_of_the_purchase_already_counts_it()
    {
        // Comprar hoy significa tener hoy. Contarlo desde mañana dejaría un día en el
        // que el dinero salió y todavía no había nada a cambio.
        var days = Build(
            [Buy(Bitcoin, on: 5, quantity: 2m, cost: 100m)],
            Prices(Bitcoin, 60m, 60m, 60m, 60m, 60m, 60m, 60m));

        Assert.Equal(Money.Euros(120m), days[4].ValueInEuros);
    }

    [Fact]
    public void A_day_after_selling_everything_has_nothing_of_that_asset()
    {
        var days = Build(
            [Buy(Bitcoin, on: 2, quantity: 2m, cost: 100m), Sell(Bitcoin, on: 5, quantity: 2m, proceeds: 130m)],
            Prices(Bitcoin, 60m, 60m, 60m, 60m, 60m, 60m, 60m));

        Assert.Equal(Money.Euros(120m), days[3].ValueInEuros);
        Assert.Empty(days[6].Assets);
    }

    [Fact]
    public void A_day_missing_one_price_gives_the_rest_and_says_it_is_incomplete()
    {
        var prices = Prices(Bitcoin, 60m, 60m, 60m, 60m, 60m, 60m, 60m);
        prices[Ether] = [new DailyPrice(Ether, Day(1), 2m, "Prueba")];

        var days = Build(
            [Buy(Bitcoin, on: 1, quantity: 1m, cost: 50m), Buy(Ether, on: 1, quantity: 10m, cost: 20m)],
            prices);

        Assert.Equal(Money.Euros(80m), days[0].ValueInEuros);
        Assert.True(days[0].IsComplete);

        // El segundo día falta el precio de ether: se da lo que se sabe y se dice.
        Assert.Equal(Money.Euros(60m), days[1].ValueInEuros);
        Assert.False(days[1].IsComplete);
        Assert.Contains(days[1].Assets, asset => asset.AssetId == Ether && asset.ValueInEuros is null);
    }

    [Fact]
    public void A_day_without_any_price_is_kept_in_the_series_and_marked()
    {
        // Omitirlo dejaría un salto en la gráfica que se leería como si no hubiera
        // pasado nada ese día.
        var days = Build(
            [Buy(Bitcoin, on: 1, quantity: 1m, cost: 50m)],
            new Dictionary<Guid, IReadOnlyList<DailyPrice>>());

        Assert.Equal(7, days.Count);
        Assert.All(days, day => Assert.False(day.IsComplete));
        Assert.All(days, day => Assert.Equal(Money.Euros(0m), day.ValueInEuros));
    }

    [Fact]
    public void Money_put_in_is_not_a_gain()
    {
        // Un ingreso seguido de una compra sube la cartera sin que nadie haya ganado
        // nada. Confundir las dos cosas convierte el ahorro en rendimiento.
        var days = Build(
            [Deposit(on: 3, amount: 1000m), Buy(Bitcoin, on: 3, quantity: 2m, cost: 1000m)],
            Prices(Bitcoin, 500m, 500m, 500m, 500m, 500m, 500m, 500m));

        Assert.Equal(Money.Euros(1000m), days[2].NetContributionInEuros);
        Assert.Equal(Money.Euros(1000m), days[2].ValueInEuros);
        Assert.Equal(Money.Euros(0m), days[3].NetContributionInEuros);
    }

    [Fact]
    public void A_withdrawal_counts_against_what_was_put_in()
    {
        var days = Build(
            [Deposit(on: 2, amount: 500m), Withdraw(on: 4, amount: 200m)],
            new Dictionary<Guid, IReadOnlyList<DailyPrice>>());

        Assert.Equal(Money.Euros(500m), days[1].NetContributionInEuros);
        Assert.Equal(Money.Euros(-200m), days[3].NetContributionInEuros);
    }

    [Fact]
    public void A_reward_adds_units_without_money_going_in()
    {
        var days = Build(
            [Reward(Bitcoin, on: 2, quantity: 1m, value: 60m)],
            Prices(Bitcoin, 60m, 60m, 60m, 60m, 60m, 60m, 60m));

        Assert.Equal(Money.Euros(60m), days[2].ValueInEuros);
        Assert.Equal(Money.Euros(0m), days[2].NetContributionInEuros);
    }

    [Fact]
    public void An_unresolved_movement_stays_out_of_the_series()
    {
        var days = Build(
            [Unknown(Bitcoin, on: 2, quantity: 5m)],
            Prices(Bitcoin, 60m, 60m, 60m, 60m, 60m, 60m, 60m));

        Assert.All(days, day => Assert.Empty(day.Assets));
    }

    private static IReadOnlyList<PortfolioDay> Build(
        IReadOnlyList<ValuedTransaction> transactions,
        IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>> prices) =>
        PortfolioHistory.Build(transactions, prices, Day(1), Day(7));

    private static DateOnly Day(int day) => new(2026, 3, day);

    private static Dictionary<Guid, IReadOnlyList<DailyPrice>> Prices(Guid assetId, params decimal[] daily) =>
        new()
        {
            [assetId] = [.. daily.Select((price, index) => new DailyPrice(assetId, Day(index + 1), price, "Prueba"))],
        };

    private static ValuedTransaction Buy(Guid assetId, int on, decimal quantity, decimal cost) =>
        Valued(TransactionType.Buy, assetId, on, quantity, cost);

    private static ValuedTransaction Sell(Guid assetId, int on, decimal quantity, decimal proceeds) =>
        Valued(TransactionType.Sell, assetId, on, quantity, proceeds);

    private static ValuedTransaction Reward(Guid assetId, int on, decimal quantity, decimal value) =>
        Valued(TransactionType.Reward, assetId, on, quantity, value);

    private static ValuedTransaction Unknown(Guid assetId, int on, decimal quantity) =>
        Valued(TransactionType.Unknown, assetId, on, quantity, 0m);

    private static ValuedTransaction Deposit(int on, decimal amount) =>
        Valued(TransactionType.Deposit, null, on, 0m, amount);

    private static ValuedTransaction Withdraw(int on, decimal amount) =>
        Valued(TransactionType.Withdrawal, null, on, 0m, amount);

    private static ValuedTransaction Valued(
        TransactionType type,
        Guid? assetId,
        int on,
        decimal quantity,
        decimal gross)
    {
        var transaction = Transaction.Imported(
            Owner,
            Guid.NewGuid(),
            type,
            assetId,
            new Quantity(quantity),
            null,
            Money.Euros(gross),
            Money.Euros(0m),
            Occurrence.FromOffset(new DateTimeOffset(Day(on).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero), "UTC"),
            TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString()));

        return ValuedTransaction.From(transaction);
    }
}
