using Kapea.Application.Abstractions;
using Kapea.Application.MarketData;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Application.Tests.MarketData;

public class PriceHistoryUpdaterTests
{
    private static readonly Guid Bitcoin = Guid.NewGuid();
    private static readonly DateTimeOffset Today = new(2026, 3, 10, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_asset_with_no_series_is_asked_for_from_its_first_acquisition()
    {
        var provider = new Provider();

        await Updater(provider, stored: null).UpdateAsync();

        var asked = Assert.Single(provider.Asked);

        Assert.Equal(new DateOnly(2026, 1, 20), asked.From);
        Assert.Equal(new DateOnly(2026, 3, 10), asked.To);
    }

    [Fact]
    public async Task A_series_already_started_is_only_asked_for_what_is_missing()
    {
        var provider = new Provider();

        await Updater(provider, stored: new DateOnly(2026, 3, 8)).UpdateAsync();

        Assert.Equal(new DateOnly(2026, 3, 9), Assert.Single(provider.Asked).From);
    }

    [Fact]
    public async Task A_series_already_up_to_date_asks_for_nothing()
    {
        // Es lo que permite ejecutarlo cada pocas horas sin agotar la cuota gratuita.
        var provider = new Provider();

        var update = await Updater(provider, stored: new DateOnly(2026, 3, 10)).UpdateAsync();

        Assert.Empty(provider.Asked);
        Assert.Equal(0, update.DaysWritten);
    }

    [Fact]
    public async Task What_the_provider_gives_is_stored()
    {
        var provider = new Provider(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 10));
        var store = new Store();

        var update = await Updater(provider, store, stored: new DateOnly(2026, 3, 8)).UpdateAsync();

        Assert.Equal(2, update.DaysWritten);
        Assert.Equal(2, store.Written.Count);
    }

    [Fact]
    public async Task An_asset_no_provider_covers_is_counted_and_does_not_stop_the_rest()
    {
        var provider = new Provider();
        var store = new Store();

        var update = await Updater(provider, store, stored: null).UpdateAsync();

        Assert.Equal(1, update.AssetsWithoutCoverage);
        Assert.Empty(store.Written);
    }

    [Fact]
    public async Task What_was_downloaded_before_a_failure_stays_stored()
    {
        // El proveedor falla con el segundo activo. Lo del primero ya está guardado, y
        // lo que falta se vuelve a pedir en la siguiente vuelta.
        var other = Guid.NewGuid();
        var store = new Store();

        var updater = new PriceHistoryUpdater(
            new Assets([
                new PricedAsset(Bitcoin, "BTC", AssetClass.Crypto, new DateOnly(2026, 1, 20)),
                new PricedAsset(other, "ETH", AssetClass.Crypto, new DateOnly(2026, 1, 20)),
            ]),
            store,
            new FailingProvider(other, new DateOnly(2026, 3, 10)),
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => updater.UpdateAsync());

        Assert.Single(store.Written);
    }

    private static FakeTimeProvider Clock()
    {
        var clock = new FakeTimeProvider();
        clock.SetUtcNow(Today);

        return clock;
    }

    private static PriceHistoryUpdater Updater(IPriceHistoryProvider provider, DateOnly? stored) =>
        Updater(provider, new Store(), stored);

    private static PriceHistoryUpdater Updater(IPriceHistoryProvider provider, Store store, DateOnly? stored)
    {
        if (stored is { } day)
        {
            store.Last[Bitcoin] = day;
        }

        return new PriceHistoryUpdater(
            new Assets([new PricedAsset(Bitcoin, "BTC", AssetClass.Crypto, new DateOnly(2026, 1, 20))]),
            store,
            provider,
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance);
    }

    /// <summary>Ingesta de tipos que no sale a ningún sitio.</summary>
    private static ExchangeRateIngestion Ingestion() =>
        new(new NoRates(), new NoRateStore(), NullLogger<ExchangeRateIngestion>.Instance);

    private sealed class NoRates : IExchangeRateSource
    {
        public string Name => "Prueba";

        public Task<IReadOnlyList<Kapea.Domain.Exchange.DailyRate>> FetchAsync(
            DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Kapea.Domain.Exchange.DailyRate>>([]);
    }

    private sealed class NoRateStore : IExchangeRateStore
    {
        public Task<IReadOnlyList<Kapea.Domain.Exchange.DailyRate>> GetOnOrBeforeAsync(
            Kapea.Domain.ValueObjects.Currency currency,
            DateOnly date,
            int maximumLookbackDays,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Kapea.Domain.Exchange.DailyRate>>([]);

        public Task<int> UpsertAsync(
            IReadOnlyList<Kapea.Domain.Exchange.DailyRate> rates, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class Assets(IReadOnlyList<PricedAsset> assets) : IPricedAssetRepository
    {
        public Task<IReadOnlyList<PricedAsset>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(assets);
    }

    private sealed class Provider(params DateOnly[] days) : IPriceHistoryProvider
    {
        public string Name => "Prueba";

        internal List<PriceHistoryRequest> Asked { get; } = [];

        public Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
            PriceHistoryRequest request,
            CancellationToken cancellationToken = default)
        {
            Asked.Add(request);

            return Task.FromResult<IReadOnlyList<DailyPrice>>(
                [.. days.Select(day => new DailyPrice(request.AssetId, day, 100m, "Prueba"))]);
        }
    }

    private sealed class FailingProvider(Guid failFor, params DateOnly[] days) : IPriceHistoryProvider
    {
        public string Name => "Prueba";

        public Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
            PriceHistoryRequest request,
            CancellationToken cancellationToken = default) =>
            request.AssetId == failFor
                ? throw new HttpRequestException("El proveedor ha dejado de responder.")
                : Task.FromResult<IReadOnlyList<DailyPrice>>(
                    [.. days.Select(day => new DailyPrice(request.AssetId, day, 100m, "Prueba"))]);
    }

    private sealed class Store : IPriceHistoryStore
    {
        internal Dictionary<Guid, DateOnly> Last { get; } = [];

        internal List<DailyPrice> Written { get; } = [];

        public Task<IReadOnlyList<DailyPrice>> GetAsync(
            Guid assetId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyPrice>>([]);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>>> GetAsync(
            IReadOnlyCollection<Guid> assetIds,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<DailyPrice>>>(
                new Dictionary<Guid, IReadOnlyList<DailyPrice>>());

        public Task<IReadOnlyDictionary<Guid, DateOnly>> GetLastStoredDayAsync(
            IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, DateOnly>>(Last);

        public Task<int> UpsertAsync(IReadOnlyList<DailyPrice> prices, CancellationToken cancellationToken = default)
        {
            Written.AddRange(prices);

            return Task.FromResult(prices.Count);
        }
    }
}
