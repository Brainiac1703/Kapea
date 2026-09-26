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
    public async Task A_series_that_starts_later_than_the_first_acquisition_is_filled_backwards()
    {
        // Pasa cuando la serie se descargó con un proveedor que entonces no llegaba tan
        // atrás. Mirando solo el final, ese hueco no se vería nunca.
        var provider = new Provider();
        var store = new Store();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 10));

        await new PriceHistoryUpdater(
            new Assets([new PricedAsset(Bitcoin, "BTC", AssetClass.Crypto, new DateOnly(2026, 1, 20))]),
            store,
            provider,
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance).UpdateAsync();

        var asked = Assert.Single(provider.Asked);

        Assert.Equal(new DateOnly(2026, 1, 20), asked.From);
        Assert.Equal(new DateOnly(2026, 1, 31), asked.To);
    }

    [Fact]
    public async Task An_asset_already_sold_is_asked_for_only_until_the_day_it_was_sold()
    {
        // Sigue necesitando los precios de cuando se tenía, o la gráfica de aquellos
        // meses saldría corta. Lo que no necesita es el precio de hoy.
        var provider = new Provider();

        await new PriceHistoryUpdater(
            new Assets([
                new PricedAsset(
                    Bitcoin,
                    "BTC",
                    AssetClass.Crypto,
                    new DateOnly(2026, 1, 20),
                    LastHeldOn: new DateOnly(2026, 2, 15)),
            ]),
            new Store(),
            provider,
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance).UpdateAsync();

        Assert.Equal(new DateOnly(2026, 2, 15), Assert.Single(provider.Asked).To);
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
    public async Task An_asset_up_to_date_except_for_today_is_not_reported_as_uncovered()
    {
        // Hoy todavía no ha cerrado. Contarlo como falta de cobertura haría parecer rota
        // una serie que está al día.
        var provider = new Provider();
        var store = new Store();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 9));

        var update = await new PriceHistoryUpdater(
            new Assets([new PricedAsset(Bitcoin, "BTC", AssetClass.Crypto, new DateOnly(2026, 1, 20))]),
            store,
            provider,
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance).UpdateAsync();

        Assert.Equal(0, update.AssetsWithoutCoverage);
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

    [Fact]
    public async Task History_below_what_was_already_asked_for_is_asked_for_once()
    {
        // El caso que obliga a recordar lo pedido: se pide desde 2000, el proveedor sólo
        // tiene desde 2026, y sin memoria el hueco de veintiséis años volvería a pedirse
        // en cada vuelta sin que nada lo delatara.
        var provider = new Provider();
        var store = new Store();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 10));
        store.Reached[Bitcoin] = new PriceHistoryReach(
            Bitcoin, new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 10), null);

        var updater = Wanting(provider, store, new DateOnly(2000, 1, 1));

        await updater.UpdateAsync();

        var first = Assert.Single(provider.Asked);

        Assert.Equal(new DateOnly(2000, 1, 1), first.From);
        Assert.Equal(new DateOnly(2026, 1, 19), first.To);

        // Segunda vuelta: ya se pidió, aunque no viniera nada.
        provider.Asked.Clear();
        await Wanting(provider, store, new DateOnly(2000, 1, 1)).UpdateAsync();

        Assert.Empty(provider.Asked);
    }

    [Fact]
    public async Task Lowering_the_floor_again_only_asks_for_the_new_stretch()
    {
        var provider = new Provider();
        var store = new Store();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 10));
        store.Reached[Bitcoin] = new PriceHistoryReach(
            Bitcoin, new DateOnly(2020, 1, 1), new DateOnly(2026, 3, 10), null);

        await Wanting(provider, store, new DateOnly(2010, 1, 1)).UpdateAsync();

        var asked = Assert.Single(provider.Asked);

        Assert.Equal(new DateOnly(2010, 1, 1), asked.From);
        Assert.Equal(new DateOnly(2019, 12, 31), asked.To);
    }

    [Fact]
    public async Task What_was_asked_for_with_another_provider_identifier_does_not_count()
    {
        // El identificador puede llegar después, al seguir el activo desde una búsqueda.
        // Desde entonces se pregunta por otra cosa, así que lo pedido antes no dice nada.
        var provider = new Provider();
        var store = new Store();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 10));
        store.Reached[Bitcoin] = new PriceHistoryReach(
            Bitcoin, new DateOnly(2000, 1, 1), new DateOnly(2026, 3, 10), "otro-identificador");

        await Wanting(provider, store, new DateOnly(2000, 1, 1)).UpdateAsync();

        Assert.Equal(new DateOnly(2000, 1, 1), Assert.Single(provider.Asked).From);
    }

    [Fact]
    public async Task An_asset_whose_history_starts_later_is_not_counted_as_uncovered()
    {
        // Sin cobertura es no tener ni un día. Un activo que empezó a cotizar después
        // del suelo tiene toda la serie que puede tener.
        var provider = new Provider(new DateOnly(2026, 2, 1));
        var store = new Store();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 10));

        var update = await Wanting(provider, store, new DateOnly(2000, 1, 1)).UpdateAsync();

        Assert.Equal(0, update.AssetsWithoutCoverage);
    }

    [Fact]
    public async Task An_asset_with_no_day_at_all_is_still_counted_as_uncovered()
    {
        var update = await Wanting(new Provider(), new Store(), new DateOnly(2000, 1, 1)).UpdateAsync();

        Assert.Equal(1, update.AssetsWithoutCoverage);
    }

    [Fact]
    public async Task Everything_is_brought_up_to_date_before_any_backfill_starts()
    {
        // Rellenar activo por activo dejaría al último sin precio de hoy hasta terminar
        // los anteriores, y sin ninguno si la cuota se agota antes.
        var provider = new Provider();
        var store = new Store();
        var second = Guid.NewGuid();

        foreach (var asset in new[] { Bitcoin, second })
        {
            store.Covered[asset] = new StoredRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 8));
            store.Reached[asset] = new PriceHistoryReach(
                asset, new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 8), null);
        }

        await new PriceHistoryUpdater(
            new Assets(
            [
                Priced(Bitcoin, "BTC", new DateOnly(2000, 1, 1)),
                Priced(second, "ETH", new DateOnly(2000, 1, 1)),
            ]),
            store,
            provider,
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance).UpdateAsync();

        // Las dos puestas al día primero, y sólo después los dos rellenos.
        Assert.Equal(
            [false, false, true, true],
            provider.Asked.Select(request => request.From < new DateOnly(2026, 1, 20)));
    }

    [Fact]
    public async Task A_provider_that_stops_responding_keeps_what_was_already_downloaded()
    {
        var store = new Store();
        var second = Guid.NewGuid();

        store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 8));
        store.Reached[Bitcoin] = new PriceHistoryReach(
            Bitcoin, new DateOnly(2026, 1, 20), new DateOnly(2026, 3, 8), null);

        var updater = new PriceHistoryUpdater(
            new Assets(
            [
                Priced(Bitcoin, "BTC", new DateOnly(2000, 1, 1)),
                Priced(second, "ETH", new DateOnly(2000, 1, 1)),
            ]),
            store,
            new FailingProvider(second, new DateOnly(2026, 3, 9)),
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(() => updater.UpdateAsync());

        // Lo del primer activo está guardado, y consta pedido para no repetirlo.
        Assert.NotEmpty(store.Written);
        Assert.True(store.Reached.ContainsKey(Bitcoin));
    }

    [Fact]
    public async Task Exchange_rates_are_ensured_down_to_the_floor_and_not_to_the_minimum()
    {
        // Un precio en dólares sin el tipo de ese día se queda fuera de la serie. Pedir
        // los tipos más tarde que los precios cortaría la historia de todo lo que cotiza
        // en dólares, y parecería culpa del proveedor de precios.
        var rates = new RecordingRates();

        await new PriceHistoryUpdater(
            new Assets([Priced(Bitcoin, "BTC", new DateOnly(2000, 1, 1))]),
            new Store(),
            new Provider(),
            new ExchangeRateIngestion(rates, new NoRateStore(), NullLogger<ExchangeRateIngestion>.Instance),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance).UpdateAsync();

        Assert.Equal(new DateOnly(2000, 1, 1), Assert.Single(rates.Asked).From);
    }

    /// <summary>El actualizador del activo de siempre, con un suelo al que llegar.</summary>
    private static PriceHistoryUpdater Wanting(IPriceHistoryProvider provider, Store store, DateOnly from) =>
        new(
            new Assets([Priced(Bitcoin, "BTC", from)]),
            store,
            provider,
            Ingestion(),
            Clock(),
            NullLogger<PriceHistoryUpdater>.Instance);

    private static PricedAsset Priced(Guid id, string symbol, DateOnly wanted) =>
        new(id, symbol, AssetClass.Crypto, new DateOnly(2026, 1, 20), null, null, wanted);

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
            store.Covered[Bitcoin] = new StoredRange(new DateOnly(2026, 1, 20), day);
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

    /// <summary>Fuente de tipos que apunta desde cuándo se le piden.</summary>
    private sealed class RecordingRates : IExchangeRateSource
    {
        public string Name => "Prueba";

        internal List<(DateOnly From, DateOnly To)> Asked { get; } = [];

        public Task<IReadOnlyList<Kapea.Domain.Exchange.DailyRate>> FetchAsync(
            DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        {
            Asked.Add((from, to));

            return Task.FromResult<IReadOnlyList<Kapea.Domain.Exchange.DailyRate>>([]);
        }
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
        internal Dictionary<Guid, StoredRange> Covered { get; } = [];

        internal Dictionary<Guid, PriceHistoryReach> Reached { get; } = [];

        internal List<DailyPrice> Written { get; } = [];

        public Task<IReadOnlyDictionary<Guid, PriceHistoryReach>> GetReachAsync(
            IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, PriceHistoryReach>>(Reached);

        public Task RecordReachAsync(PriceHistoryReach reach, CancellationToken cancellationToken = default)
        {
            Reached[reach.AssetId] = reach;

            return Task.CompletedTask;
        }

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

        public Task<IReadOnlyDictionary<Guid, StoredRange>> GetStoredRangeAsync(
            IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, StoredRange>>(Covered);

        public Task<int> UpsertAsync(IReadOnlyList<DailyPrice> prices, CancellationToken cancellationToken = default)
        {
            Written.AddRange(prices);

            return Task.FromResult(prices.Count);
        }
    }
}
