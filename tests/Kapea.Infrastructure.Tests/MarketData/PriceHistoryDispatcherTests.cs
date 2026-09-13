using Kapea.Application.Abstractions;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Kapea.Infrastructure.MarketData;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.MarketData;

public class PriceHistoryDispatcherTests
{
    private static readonly Guid Asset = Guid.NewGuid();

    [Fact]
    public async Task The_first_provider_that_covers_the_range_settles_it()
    {
        var yahoo = new Provider("Yahoo", Days(1, 2, 3));
        var coinGecko = new Provider("CoinGecko", Days(1, 2, 3));

        var prices = await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request());

        Assert.Equal(3, prices.Count);
        Assert.All(prices, price => Assert.Equal("Yahoo", price.Source));

        // Al segundo ni se le pregunta: gastar su cuota en días que ya se tienen deja
        // sin margen para los activos que solo él cubre.
        Assert.Empty(coinGecko.Asked);
    }

    [Fact]
    public async Task The_second_provider_is_only_asked_for_what_is_missing()
    {
        var yahoo = new Provider("Yahoo", Days(1, 2));
        var coinGecko = new Provider("CoinGecko", Days(3));

        var prices = await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request());

        Assert.Equal(3, prices.Count);
        Assert.Equal(new DateOnly(2026, 3, 3), Assert.Single(coinGecko.Asked).From);
    }

    [Fact]
    public async Task An_asset_no_one_covers_comes_back_empty_and_not_broken()
    {
        var yahoo = new Provider("Yahoo", []);
        var coinGecko = new Provider("CoinGecko", []);

        Assert.Empty(await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request()));
    }

    [Fact]
    public async Task The_series_comes_back_in_order_of_date()
    {
        var yahoo = new Provider("Yahoo", Days(3));
        var coinGecko = new Provider("CoinGecko", Days(1, 2));

        var prices = await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request());

        Assert.Equal([.. prices.Select(price => price.Date).Order()], [.. prices.Select(price => price.Date)]);
    }

    private static PriceHistoryDispatcher Dispatcher(params IPriceHistoryProvider[] providers) =>
        new(providers, NullLogger<PriceHistoryDispatcher>.Instance);

    private static PriceHistoryRequest Request() =>
        new(Asset, "BTC", AssetClass.Crypto, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));

    private static int[] Days(params int[] days) => days;

    private sealed class Provider(string name, int[] days) : IPriceHistoryProvider
    {
        public string Name => name;

        internal List<PriceHistoryRequest> Asked { get; } = [];

        public Task<IReadOnlyList<DailyPrice>> GetHistoryAsync(
            PriceHistoryRequest request,
            CancellationToken cancellationToken = default)
        {
            Asked.Add(request);

            var prices = days
                .Select(day => new DateOnly(2026, 3, day))
                .Where(date => date >= request.From && date <= request.To)
                .Select(date => new DailyPrice(Asset, date, 100m, name))
                .ToList();

            return Task.FromResult<IReadOnlyList<DailyPrice>>(prices);
        }
    }
}
