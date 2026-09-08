using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Calculation;
using Kapea.Domain.Lots;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class PortfolioProjectionStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_recalculation_replaces_the_whole_projection_without_leaving_leftovers()
    {
        var scenario = await NewScenarioAsync();

        await ReplaceAsync(scenario, Projection(scenario, lotQuantity: 10m, realizedResults: 1));
        await ReplaceAsync(scenario, Projection(scenario, lotQuantity: 4m, realizedResults: 0));

        await using var context = fixture.CreateContext(scenario.Owner);

        var lot = Assert.Single(await context.Lots.Where(lot => lot.AssetId == scenario.AssetId).ToListAsync());

        Assert.Equal(new Quantity(4m), lot.RemainingQuantity);
        Assert.Empty(await context.RealizedResults.Where(r => r.AssetId == scenario.AssetId).ToListAsync());
    }

    [Fact]
    public async Task The_breakdown_of_a_result_survives_the_round_trip()
    {
        var scenario = await NewScenarioAsync();

        await ReplaceAsync(scenario, Projection(scenario, lotQuantity: 10m, realizedResults: 1));

        await using var context = fixture.CreateContext(scenario.Owner);

        var result = await context.RealizedResults
            .Include(realized => realized.ConsumedLots)
            .SingleAsync(realized => realized.AssetId == scenario.AssetId);

        var consumed = Assert.Single(result.ConsumedLots);

        Assert.Equal(Money.Euros(1400m), result.ProceedsInEuros);
        Assert.Equal(Money.Euros(1000m), result.AcquisitionCostInEuros);
        Assert.Equal(Money.Euros(400m), result.ResultInEuros);
        Assert.Equal(Money.Euros(1000m), consumed.AcquisitionCostInEuros);
        Assert.Equal("Europe/Madrid", consumed.AcquiredAt.SourceTimeZoneId);
    }

    [Fact]
    public async Task Decimal_precision_survives_the_round_trip()
    {
        // Ocho decimales de bitcoin: si la columna los recortara, el coste del lote
        // dejaría de cuadrar con el movimiento del que salió.
        var scenario = await NewScenarioAsync();
        var quantity = 0.12345678m;

        await ReplaceAsync(scenario, Projection(scenario, lotQuantity: quantity, realizedResults: 0));

        await using var context = fixture.CreateContext(scenario.Owner);
        var lot = await context.Lots.SingleAsync(lot => lot.AssetId == scenario.AssetId);

        Assert.Equal(quantity, lot.RemainingQuantity.Value);
    }

    [Fact]
    public async Task Replacing_one_asset_does_not_touch_another()
    {
        var scenario = await NewScenarioAsync();
        var otherAsset = await NewAssetAsync();

        await ReplaceAsync(scenario, Projection(scenario, lotQuantity: 10m, realizedResults: 0));
        await ReplaceAsync(scenario with { AssetId = otherAsset }, Projection(scenario with { AssetId = otherAsset }, 7m, 0));
        await ReplaceAsync(scenario, Projection(scenario, lotQuantity: 3m, realizedResults: 0));

        await using var context = fixture.CreateContext(scenario.Owner);

        Assert.Equal(new Quantity(7m), (await context.Lots.SingleAsync(lot => lot.AssetId == otherAsset)).RemainingQuantity);
        Assert.Equal(new Quantity(3m), (await context.Lots.SingleAsync(lot => lot.AssetId == scenario.AssetId)).RemainingQuantity);
    }

    [Fact]
    public async Task A_recalculation_does_not_reach_another_users_projection()
    {
        var mine = await NewScenarioAsync();
        var theirs = await NewScenarioAsync(mine.AssetId);

        await ReplaceAsync(theirs, Projection(theirs, lotQuantity: 9m, realizedResults: 0));
        await ReplaceAsync(mine, Projection(mine, lotQuantity: 2m, realizedResults: 0));

        await using var context = fixture.CreateContext(theirs.Owner);

        Assert.Equal(new Quantity(9m), (await context.Lots.SingleAsync(lot => lot.AssetId == theirs.AssetId)).RemainingQuantity);
    }

    private async Task ReplaceAsync(Scenario scenario, AssetCalculationResult projection)
    {
        await using var context = fixture.CreateContext(scenario.Owner);

        await new PortfolioProjectionStore(context, NullLogger<PortfolioProjectionStore>.Instance)
            .ReplaceAsync(scenario.AssetId, projection);
    }

    private static AssetCalculationResult Projection(Scenario scenario, decimal lotQuantity, int realizedResults)
    {
        var acquiredAt = Occurrence.FromNaive(new DateTime(2024, 1, 10, 9, 0, 0), "Europe/Madrid");
        var lot = Lot.Create(
            scenario.Owner, scenario.AssetId, scenario.AccountId, Guid.NewGuid(),
            new Quantity(lotQuantity), Money.Euros(1000m), acquiredAt, sequenceNumber: 1);

        var realized = realizedResults == 0
            ? []
            : new List<RealizedResult>
            {
                new(
                    scenario.Owner, scenario.AssetId, Guid.NewGuid(), scenario.AccountId,
                    Occurrence.FromNaive(new DateTime(2025, 2, 1, 9, 0, 0), "Europe/Madrid"),
                    new Quantity(10m), Money.Euros(1400m), Money.Euros(1000m),
                    [new ConsumedLot(lot.Id, Guid.NewGuid(), acquiredAt, new Quantity(10m), Money.Euros(1000m), Money.Euros(1400m))]),
            };

        return new AssetCalculationResult(scenario.AssetId, [lot], realized, [], [], 0);
    }

    private async Task<Scenario> NewScenarioAsync(Guid? assetId = null)
    {
        var owner = new UserId(Guid.NewGuid());
        var account = PlatformAccount.Create(owner, Platform.Kraken, "Cuenta", Currency.Euro);

        await using var context = fixture.CreateContext(owner);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return new Scenario(owner, assetId ?? await NewAssetAsync(), account.Id);
    }

    private async Task<Guid> NewAssetAsync()
    {
        var asset = Asset.Create("SYM" + Random.Shared.Next(1000000), AssetClass.Crypto);

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));
        context.Assets.Add(asset);
        await context.SaveChangesAsync();

        return asset.Id;
    }

    private sealed record Scenario(UserId Owner, Guid AssetId, Guid AccountId);
}
