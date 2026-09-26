using Kapea.Application.MarketData;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kapea.Infrastructure.Tests.Persistence;

/// <summary>
/// Hasta dónde se ha pedido la serie de cada activo, y hasta dónde se quiere llegar.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class PriceHistoryReachTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task What_is_already_downloaded_counts_as_already_asked_for()
    {
        // Es lo que hace la migración. Sin ello, la primera ejecución tras migrar
        // volvería a pedir la serie entera de cada activo para descubrir que ya la tiene.
        var owner = new UserId(Guid.NewGuid());
        var asset = await AssetWithPricesAsync(owner, "AAA", providerId: "aaa-coin");

        await using var context = fixture.CreateContext(owner);
        await context.Database.ExecuteSqlRawAsync(PriceHistoryReachSeed.Sql);

        var reach = await context.PriceHistoryReaches.SingleAsync(entry => entry.AssetId == asset);

        Assert.Equal(new DateOnly(2026, 1, 10), reach.RequestedFrom);
        Assert.Equal(new DateOnly(2026, 1, 12), reach.RequestedTo);
        Assert.Equal("aaa-coin", reach.RequestedWith);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_nor_overwrite()
    {
        var owner = new UserId(Guid.NewGuid());
        var asset = await AssetWithPricesAsync(owner, "BBB", providerId: null);

        await using var context = fixture.CreateContext(owner);
        await context.Database.ExecuteSqlRawAsync(PriceHistoryReachSeed.Sql);

        var store = new PriceHistoryStore(context);
        await store.RecordReachAsync(
            PriceHistoryReach.Of(asset, new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 12), null));

        await context.Database.ExecuteSqlRawAsync(PriceHistoryReachSeed.Sql);

        var reach = await context.PriceHistoryReaches.SingleAsync(entry => entry.AssetId == asset);

        Assert.Equal(new DateOnly(2000, 1, 1), reach.RequestedFrom);
    }

    [Fact]
    public async Task What_was_asked_for_only_grows()
    {
        var owner = new UserId(Guid.NewGuid());
        var asset = Guid.NewGuid();

        await using var context = fixture.CreateContext(owner);
        var store = new PriceHistoryStore(context);

        await store.RecordReachAsync(
            PriceHistoryReach.Of(asset, new DateOnly(2020, 1, 1), new DateOnly(2026, 1, 1), null));
        await store.RecordReachAsync(
            PriceHistoryReach.Of(asset, new DateOnly(2024, 1, 1), new DateOnly(2026, 3, 1), null));

        var reach = Assert.Single(await store.GetReachAsync([asset]));

        Assert.Equal(new DateOnly(2020, 1, 1), reach.Value.RequestedFrom);
        Assert.Equal(new DateOnly(2026, 3, 1), reach.Value.RequestedTo);
    }

    [Fact]
    public async Task Asking_with_another_provider_identifier_replaces_what_was_asked_for()
    {
        // Se está preguntando por otro activo del proveedor: lo anterior no dice nada.
        var owner = new UserId(Guid.NewGuid());
        var asset = Guid.NewGuid();

        await using var context = fixture.CreateContext(owner);
        var store = new PriceHistoryStore(context);

        await store.RecordReachAsync(
            PriceHistoryReach.Of(asset, new DateOnly(2000, 1, 1), new DateOnly(2026, 1, 1), null));
        await store.RecordReachAsync(
            PriceHistoryReach.Of(asset, new DateOnly(2024, 1, 1), new DateOnly(2026, 3, 1), "otro"));

        var reach = Assert.Single(await store.GetReachAsync([asset]));

        Assert.Equal(new DateOnly(2024, 1, 1), reach.Value.RequestedFrom);
        Assert.Equal("otro", reach.Value.RequestedWith);
    }

    [Fact]
    public async Task Every_priced_asset_wants_history_down_to_the_configured_floor()
    {
        var owner = new UserId(Guid.NewGuid());
        var watched = await WatchedAssetAsync(owner, "CCC");

        await using var context = fixture.CreateContext(owner);

        var assets = await new PricedAssetRepository(
                context,
                Options.Create(new PriceHistoryOptions { EarliestFrom = new DateOnly(1999, 12, 31) }))
            .ListAsync();

        var asset = assets.Single(entry => entry.AssetId == watched);

        // El suelo es hasta dónde se quiere llegar; el mínimo garantizado sigue siendo
        // lo que los sistemas declarados necesitan, y es mucho más reciente.
        Assert.Equal(new DateOnly(1999, 12, 31), asset.DesiredFrom);
        Assert.True(asset.FirstHeldOn > new DateOnly(2000, 1, 1));
        Assert.Equal(new DateOnly(1999, 12, 31), asset.Earliest);
    }

    private async Task<Guid> AssetWithPricesAsync(UserId owner, string symbol, string? providerId)
    {
        await using var context = fixture.CreateContext(owner);

        var asset = Asset.Create(symbol, AssetClass.Crypto);

        if (providerId is not null)
        {
            asset.KnownAs(providerId, symbol);
        }

        context.Assets.Add(asset);
        context.DailyPrices.AddRange(
            new DailyPrice(asset.Id, new DateOnly(2026, 1, 10), 1m, "Prueba"),
            new DailyPrice(asset.Id, new DateOnly(2026, 1, 11), 1m, "Prueba"),
            new DailyPrice(asset.Id, new DateOnly(2026, 1, 12), 1m, "Prueba"));

        await context.SaveChangesAsync();

        return asset.Id;
    }

    private async Task<Guid> WatchedAssetAsync(UserId owner, string symbol)
    {
        await using var context = fixture.CreateContext(owner);

        var asset = Asset.Create(symbol, AssetClass.Crypto);
        context.Assets.Add(asset);
        context.WatchedAssets.Add(WatchedAsset.Of(owner, asset.Id, DateTimeOffset.UtcNow));

        await context.SaveChangesAsync();

        return asset.Id;
    }
}
