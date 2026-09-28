using Kapea.Application.Abstractions;
using Kapea.Application.Watchlist;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Kapea.Shared.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Application.Tests.Watchlist;

/// <summary>
/// La columna de precio de la lista no sale del histórico sino de la cotización, y la
/// cobertura de los dos no coincide. Preguntar sólo por una daba por bueno un activo que
/// después aparecía sin precio, y eso es lo que estas pruebas impiden que vuelva.
/// </summary>
public class WatchlistCoverageTests
{
    [Fact]
    public async Task An_asset_covered_by_both_providers_is_added_without_warnings()
    {
        var added = await Service(series: true, quote: true).AddAsync("BTC", AssetClass.Crypto);

        Assert.True(added.HasSeries);
        Assert.True(added.HasQuote);
    }

    [Fact]
    public async Task An_asset_with_history_but_no_quote_says_which_one_is_missing()
    {
        // El caso real: QNT tenía 2.971 días de histórico y ni una cotización, y el alta
        // decía que todo estaba bien.
        var added = await Service(series: true, quote: false).AddAsync("QNT", AssetClass.Crypto);

        Assert.True(added.HasSeries);
        Assert.False(added.HasQuote);
    }

    [Fact]
    public async Task An_asset_with_a_quote_but_no_history_says_which_one_is_missing()
    {
        var added = await Service(series: false, quote: true).AddAsync("NUEVO", AssetClass.Crypto);

        Assert.False(added.HasSeries);
        Assert.True(added.HasQuote);
    }

    [Fact]
    public async Task An_asset_nobody_covers_is_still_added()
    {
        // No bloquea el alta: lo que hoy no tiene precio puede tenerlo mañana.
        var added = await Service(series: false, quote: false).AddAsync("INVENTADO", AssetClass.Crypto);

        Assert.False(added.HasSeries);
        Assert.False(added.HasQuote);
        Assert.NotNull(added.Asset);
    }

    [Fact]
    public async Task Following_from_a_search_checks_the_same_two_things()
    {
        // Las dos rutas de alta comparten la comprobación: la que pasó desapercibida fue
        // la del símbolo, pero la de la búsqueda tenía el mismo agujero.
        var added = await Service(series: true, quote: false).AddAsync(
            new AssetSearchResult("QNT", "Quant", AssetClass.Crypto, "quant-network", "CoinGecko"));

        Assert.True(added.HasSeries);
        Assert.False(added.HasQuote);
    }

    [Fact]
    public async Task A_provider_that_throws_counts_as_no_coverage_and_does_not_break_the_add()
    {
        var added = await Service(series: true, quote: false, quotesThrow: true)
            .AddAsync("BTC", AssetClass.Crypto);

        Assert.False(added.HasQuote);
        Assert.NotNull(added.Asset);
    }

    private static WatchlistService Service(bool series, bool quote, bool quotesThrow = false) =>
        new(
            new Repository(),
            new History(series),
            new Quotes(quote, quotesThrow),
            new FakeTimeProvider(),
            NullLogger<WatchlistService>.Instance);

    private sealed class History(bool covers) : IPriceHistoryProvider
    {
        public string Name => "Prueba";

        public Task<PriceHistoryResult> GetHistoryAsync(
            PriceHistoryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(covers
                ? PriceHistoryResult.Of([new DailyPrice(request.AssetId, request.To, 100m, "Prueba")])
                : PriceHistoryResult.Nothing);
    }

    private sealed class Quotes(bool covers, bool throws) : IMarketPriceProvider
    {
        public Task<IReadOnlyDictionary<string, MarketPrice>> GetPricesAsync(
            IReadOnlyCollection<QuotedAsset> assets, CancellationToken cancellationToken = default)
        {
            if (throws)
            {
                throw new HttpRequestException("El proveedor ha dejado de responder.");
            }

            var symbol = assets.Single().CanonicalSymbol;

            return Task.FromResult<IReadOnlyDictionary<string, MarketPrice>>(covers
                ? new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase)
                {
                    [symbol] = new(symbol, 100m, DateTimeOffset.UnixEpoch),
                }
                : new Dictionary<string, MarketPrice>(StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>Un catálogo en memoria que acepta cualquier alta.</summary>
    private sealed class Repository : IWatchlistRepository
    {
        private readonly Dictionary<Guid, Asset> _assets = [];
        private readonly HashSet<Guid> _watched = [];

        public Task<IReadOnlyList<WatchedAssetResponse>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchedAssetResponse>>([]);

        public Task<Asset?> FindAsync(
            string symbol, AssetClass assetClass, CancellationToken cancellationToken = default) =>
            Task.FromResult(_assets.Values.FirstOrDefault(asset =>
                asset.CanonicalSymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                && asset.Class == assetClass));

        public Task<Asset> AddToCatalogueAsync(
            string symbol,
            AssetClass assetClass,
            CancellationToken cancellationToken = default,
            string? displayName = null,
            string? providerId = null)
        {
            var asset = Asset.Create(symbol, assetClass, displayName, isin: null, providerId);
            _assets[asset.Id] = asset;

            return Task.FromResult(asset);
        }

        public Task<Asset?> FindByProviderAsync(
            AssetSearchResult result, CancellationToken cancellationToken = default) =>
            Task.FromResult<Asset?>(null);

        public Task<bool> IsWatchedAsync(Guid assetId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_watched.Contains(assetId));

        public Task WatchAsync(Guid assetId, DateTimeOffset addedAt, CancellationToken cancellationToken = default)
        {
            _watched.Add(assetId);

            return Task.CompletedTask;
        }

        public Task StopWatchingAsync(Guid assetId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsHeldAsync(Guid assetId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<WatchedAssetResponse?> FindWatchedAsync(
            Guid assetId, CancellationToken cancellationToken = default)
        {
            var asset = _assets[assetId];

            return Task.FromResult<WatchedAssetResponse?>(new WatchedAssetResponse(
                asset.Id, asset.CanonicalSymbol, asset.DisplayName, asset.Class.ToString(),
                false, null, null, null, null));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
