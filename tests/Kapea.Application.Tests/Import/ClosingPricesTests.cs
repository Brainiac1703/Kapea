using Kapea.Application.Abstractions;
using Kapea.Application.Import;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Application.Tests.Import;

public class ClosingPricesTests
{
    private static readonly DateOnly Day = new(2025, 11, 30);

    [Fact]
    public async Task The_price_already_stored_is_used_without_asking_any_provider()
    {
        var asset = Asset.Create("PAXG", AssetClass.Crypto);
        var store = new MemoryStore();
        var provider = new CountingProvider(3500m);

        await store.UpsertAsync([new DailyPrice(asset.Id, Day, 3300m, "pruebas")]);

        var price = await new ClosingPrices(store, provider, NullLogger<ClosingPrices>.Instance)
            .FindAsync(asset, Day);

        Assert.Equal(3300m, price);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task What_is_downloaded_is_kept_so_the_next_valuation_does_not_hit_the_network()
    {
        // Una importación de cien permutas no puede convertirse en cien llamadas al
        // proveedor: lo descargado se guarda y alimenta también a las gráficas.
        var asset = Asset.Create("PAXG", AssetClass.Crypto);
        var store = new MemoryStore();
        var provider = new CountingProvider(3500m);
        var prices = new ClosingPrices(store, provider, NullLogger<ClosingPrices>.Instance);

        Assert.Equal(3500m, await prices.FindAsync(asset, Day));
        Assert.Equal(3500m, await prices.FindAsync(asset, Day));

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task A_day_no_provider_covers_comes_back_empty_instead_of_zero()
    {
        var asset = Asset.Create("PAXG", AssetClass.Crypto);
        var prices = new ClosingPrices(
            new MemoryStore(), new CountingProvider(null), NullLogger<ClosingPrices>.Instance);

        Assert.Null(await prices.FindAsync(asset, Day));
    }

    private sealed class CountingProvider(decimal? price) : IPriceHistoryProvider
    {
        public int Calls { get; private set; }

        public string Name => "contador";

        public Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
            PriceHistoryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Calls++;

            return Task.FromResult<IReadOnlyList<DailyPrice>>(price is { } value
                ? [new DailyPrice(request.AssetId, request.From, value, Name)]
                : []);
        }
    }

    private sealed class MemoryStore : IPriceHistoryStore
    {
        private readonly List<DailyPrice> _prices = [];

        public Task<IReadOnlyList<DailyPrice>> GetAsync(
            Guid assetId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyPrice>>(
                [.. _prices.Where(price => price.AssetId == assetId && price.Date >= from && price.Date <= to)]);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>>> GetAsync(
            IReadOnlyCollection<Guid> assetIds, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>>>(
                _prices.Where(price => assetIds.Contains(price.AssetId))
                    .GroupBy(price => price.AssetId)
                    .ToDictionary(group => group.Key, group => (IReadOnlyList<DailyPrice>)[.. group]));

        public Task<IReadOnlyDictionary<Guid, StoredRange>> GetStoredRangeAsync(
            IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, StoredRange>>(
                _prices.GroupBy(price => price.AssetId)
                    .ToDictionary(
                        group => group.Key,
                        group => new StoredRange(group.Min(price => price.Date), group.Max(price => price.Date))));

        public Task<int> UpsertAsync(IReadOnlyList<DailyPrice> prices, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(prices);
            _prices.AddRange(prices.Where(price =>
                !_prices.Any(stored => stored.AssetId == price.AssetId && stored.Date == price.Date)));

            return Task.FromResult(prices.Count);
        }
    }
}
