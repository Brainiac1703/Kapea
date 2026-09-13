using Kapea.Domain.MarketData;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence.Stores;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class PriceHistoryStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_price_is_read_back_by_asset_and_date()
    {
        var asset = Guid.NewGuid();

        await UpsertAsync(new DailyPrice(asset, new DateOnly(2026, 3, 10), 66_000m, "Yahoo"));

        var prices = await StoreAsync().GetAsync(asset, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(66_000m, Assert.Single(prices).PriceInEuros);
    }

    [Fact]
    public async Task The_same_day_is_never_stored_twice()
    {
        // Dos proveedores dan cifras que difieren en el último decimal. Sin esta clave,
        // el mismo día valdría una cosa u otra según quién sincronizara el último.
        var asset = Guid.NewGuid();
        var day = new DateOnly(2026, 3, 10);

        await UpsertAsync(new DailyPrice(asset, day, 66_000m, "Yahoo"));
        var second = await UpsertAsync(new DailyPrice(asset, day, 66_010m, "CoinGecko"));

        var prices = await StoreAsync().GetAsync(asset, day, day);

        Assert.Equal(0, second);
        Assert.Equal(66_000m, Assert.Single(prices).PriceInEuros);
    }

    [Fact]
    public async Task Mixing_new_days_with_stored_ones_leaves_one_row_per_day()
    {
        var asset = Guid.NewGuid();

        await UpsertAsync(
            new DailyPrice(asset, new DateOnly(2026, 3, 10), 66_000m, "Yahoo"),
            new DailyPrice(asset, new DateOnly(2026, 3, 11), 66_500m, "Yahoo"));

        var written = await UpsertAsync(
            new DailyPrice(asset, new DateOnly(2026, 3, 11), 66_500m, "Yahoo"),
            new DailyPrice(asset, new DateOnly(2026, 3, 12), 67_000m, "Yahoo"));

        var prices = await StoreAsync().GetAsync(asset, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(1, written);
        Assert.Equal(3, prices.Count);
        Assert.Equal([66_000m, 66_500m, 67_000m], [.. prices.Select(price => price.PriceInEuros)]);
    }

    [Fact]
    public async Task The_series_comes_back_oldest_first()
    {
        var asset = Guid.NewGuid();

        await UpsertAsync(
            new DailyPrice(asset, new DateOnly(2026, 3, 12), 67_000m, "Yahoo"),
            new DailyPrice(asset, new DateOnly(2026, 3, 10), 66_000m, "Yahoo"));

        var prices = await StoreAsync().GetAsync(asset, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal([new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 12)], [.. prices.Select(price => price.Date)]);
    }

    [Fact]
    public async Task An_asset_with_no_series_comes_back_empty_rather_than_missing()
    {
        var prices = await StoreAsync().GetAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Empty(prices);
    }

    [Fact]
    public async Task The_stored_range_says_from_where_to_where_each_series_reaches()
    {
        // Hacen falta los dos extremos: una serie que empezó tarde tiene un hueco al
        // principio que mirando solo el final no se vería nunca.
        var withSeries = Guid.NewGuid();
        var withoutSeries = Guid.NewGuid();

        await UpsertAsync(
            new DailyPrice(withSeries, new DateOnly(2026, 3, 10), 66_000m, "Yahoo"),
            new DailyPrice(withSeries, new DateOnly(2026, 3, 12), 67_000m, "Yahoo"));

        var covered = await StoreAsync().GetStoredRangeAsync([withSeries, withoutSeries]);

        Assert.Equal(new DateOnly(2026, 3, 10), covered[withSeries].First);
        Assert.Equal(new DateOnly(2026, 3, 12), covered[withSeries].Last);
        Assert.False(covered.ContainsKey(withoutSeries));
    }

    [Fact]
    public async Task Several_assets_come_back_grouped_by_asset()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await UpsertAsync(
            new DailyPrice(first, new DateOnly(2026, 3, 10), 66_000m, "Yahoo"),
            new DailyPrice(second, new DateOnly(2026, 3, 10), 2_000m, "Yahoo"),
            new DailyPrice(second, new DateOnly(2026, 3, 11), 2_100m, "Yahoo"));

        var byAsset = await StoreAsync().GetAsync([first, second], new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Single(byAsset[first]);
        Assert.Equal(2, byAsset[second].Count);
    }

    private PriceHistoryStore StoreAsync() => new(fixture.CreateContext(new UserId(Guid.NewGuid())));

    private async Task<int> UpsertAsync(params DailyPrice[] prices)
    {
        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));

        return await new PriceHistoryStore(context).UpsertAsync(prices);
    }
}
