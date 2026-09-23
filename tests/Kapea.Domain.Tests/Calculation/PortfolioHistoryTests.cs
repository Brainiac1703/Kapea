using Kapea.Domain.Assets;
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
    public void Money_moved_between_your_own_accounts_is_not_money_put_in()
    {
        // Sale de una cuenta y entra en otra: el mismo dinero de siempre. Contarlo como
        // aportación convertiría un traspaso en ahorro nuevo y hundiría el rendimiento.
        var transfer = Guid.NewGuid();

        var days = Build(
            [
                Deposit(on: 2, amount: 500m),
                Withdraw(on: 3, amount: 200m, transferId: transfer, transferDestination: Guid.NewGuid()),
                Deposit(on: 3, amount: 200m, transferId: transfer),
            ],
            new Dictionary<Guid, IReadOnlyList<DailyPrice>>());

        Assert.Equal(Money.Euros(500m), days[1].NetContributionInEuros);
        Assert.Equal(Money.Euros(0m), days[2].NetContributionInEuros);
    }

    [Fact]
    public void An_asset_arriving_from_outside_is_not_money_put_in()
    {
        // Cripto que llega de una cartera de fuera no es dinero del bolsillo. Contarla
        // como aportación hundiría el rendimiento por algo que nunca se pagó.
        //
        // Tampoco crea posición, porque no se conoce su coste: de eso avisa el resumen
        // de la cartera al decir que lo aportado se queda corto.
        var days = Build(
            [Valued(TransactionType.Deposit, Bitcoin, on: 2, quantity: 1m, gross: 0m)],
            Prices(Bitcoin, 100m, 100m, 100m, 100m, 100m, 100m, 100m));

        Assert.All(days, day => Assert.Equal(Money.Euros(0m), day.NetContributionInEuros));
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

    private static ValuedTransaction Deposit(int on, decimal amount, Guid? transferId = null) =>
        Valued(TransactionType.Deposit, null, on, 0m, amount, transferId);

    private static ValuedTransaction Withdraw(
        int on, decimal amount, Guid? transferId = null, Guid? transferDestination = null) =>
        Valued(TransactionType.Withdrawal, null, on, 0m, amount, transferId, transferDestination);

    private static ValuedTransaction Valued(
        TransactionType type,
        Guid? assetId,
        int on,
        decimal quantity,
        decimal gross,
        Guid? transferId = null,
        Guid? transferDestination = null)
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

        return ValuedTransaction.From(
            transaction,
            internalTransferId: transferId,
            transferDestinationAccountId: transferDestination);
    }
}

public class AssetAndClassHistoryTests
{
    private static readonly Guid Bitcoin = Guid.NewGuid();
    private static readonly Guid Santander = Guid.NewGuid();

    [Fact]
    public void The_series_of_one_asset_is_the_same_figure_as_in_the_total()
    {
        // Filtrar en lugar de recalcular: dos caminos distintos acaban divergiendo, y
        // entonces la suma de las posiciones deja de dar el total.
        var days = Days();

        var series = PortfolioHistory.ForAsset(days, Bitcoin);

        Assert.Equal(3, series.Count);
        Assert.Equal(Money.Euros(120m), series[1].ValueInEuros);
        Assert.Equal(new Quantity(2m), series[1].Quantity);
    }

    [Fact]
    public void A_day_in_which_the_asset_was_not_held_comes_back_at_zero_and_without_price()
    {
        var series = PortfolioHistory.ForAsset(Days(), Bitcoin);

        Assert.Equal(Quantity.Zero, series[0].Quantity);
        Assert.Null(series[0].ValueInEuros);
    }

    [Fact]
    public void Each_class_adds_up_on_its_own()
    {
        var byClass = PortfolioHistory.ByClass(Days(), new Dictionary<Guid, AssetClass>
        {
            [Bitcoin] = AssetClass.Crypto,
            [Santander] = AssetClass.Equity,
        });

        Assert.Equal(Money.Euros(120m), byClass[1].ValueByClass[AssetClass.Crypto]);
        Assert.Equal(Money.Euros(50m), byClass[1].ValueByClass[AssetClass.Equity]);
    }

    [Fact]
    public void A_class_incorporated_later_appears_from_its_first_day()
    {
        // Los grupos salen de lo que haya cada día, así que una clase nueva aparece sola
        // sin tocar nada y sin alterar los días anteriores.
        var byClass = PortfolioHistory.ByClass(Days(), new Dictionary<Guid, AssetClass>
        {
            [Bitcoin] = AssetClass.Crypto,
            [Santander] = AssetClass.Equity,
        });

        Assert.DoesNotContain(AssetClass.Equity, byClass[0].ValueByClass.Keys);
        Assert.Contains(AssetClass.Equity, byClass[1].ValueByClass.Keys);
    }

    /// <summary>Tres días: el primero vacío, y a partir del segundo dos posiciones.</summary>
    private static IReadOnlyList<PortfolioDay> Days() =>
    [
        new PortfolioDay(new DateOnly(2026, 3, 1), [], Money.Euros(0m), Money.Euros(0m), true),
        new PortfolioDay(
            new DateOnly(2026, 3, 2),
            [
                new AssetDay(Bitcoin, new Quantity(2m), Money.Euros(60m), Money.Euros(120m)),
                new AssetDay(Santander, new Quantity(10m), Money.Euros(5m), Money.Euros(50m)),
            ],
            Money.Euros(170m),
            Money.Euros(0m),
            true),
        new PortfolioDay(
            new DateOnly(2026, 3, 3),
            [new AssetDay(Bitcoin, new Quantity(2m), null, null)],
            Money.Euros(0m),
            Money.Euros(0m),
            false),
    ];
}
