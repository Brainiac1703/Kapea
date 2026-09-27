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

        var prices = (await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request())).Prices;

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

        var prices = (await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request())).Prices;

        Assert.Equal(3, prices.Count);
        Assert.Equal(new DateOnly(2026, 3, 3), Assert.Single(coinGecko.Asked).From);
    }

    [Fact]
    public async Task An_asset_no_one_covers_comes_back_empty_and_not_broken()
    {
        var yahoo = new Provider("Yahoo", []);
        var coinGecko = new Provider("CoinGecko", []);

        Assert.Empty((await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request())).Prices);
    }

    [Fact]
    public async Task The_series_comes_back_in_order_of_date()
    {
        var yahoo = new Provider("Yahoo", Days(3));
        var coinGecko = new Provider("CoinGecko", Days(1, 2));

        var prices = (await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request())).Prices;

        Assert.Equal([.. prices.Select(price => price.Date).Order()], [.. prices.Select(price => price.Date)]);
    }

    [Fact]
    public async Task A_range_every_provider_answered_is_settled()
    {
        var yahoo = new Provider("Yahoo", Days(1, 2));
        var coinGecko = new Provider("CoinGecko", Days(3));

        Assert.True((await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request())).Answered);
    }

    [Fact]
    public async Task A_range_is_not_settled_when_one_provider_failed_even_if_another_brought_days()
    {
        // Darlo por preguntado porque vino algo dejaría el tramo del que falló perdido
        // para siempre: es el mismo error que se corrige abajo, un nivel más arriba.
        var yahoo = new Provider("Yahoo", [], answers: false);
        var coinGecko = new Provider("CoinGecko", Days(3));

        var result = await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request());

        Assert.False(result.Answered);
        Assert.Single(result.Prices);
    }

    [Fact]
    public async Task A_provider_that_was_never_asked_cannot_have_failed()
    {
        var yahoo = new Provider("Yahoo", Days(1, 2, 3));
        var coinGecko = new Provider("CoinGecko", [], answers: false);

        var result = await Dispatcher(yahoo, coinGecko).GetHistoryAsync(Request());

        Assert.True(result.Answered);
        Assert.Empty(coinGecko.Asked);
    }

    [Fact]
    public async Task An_asset_no_one_covers_is_settled_and_a_provider_that_fell_over_is_not()
    {
        var covered = new Provider("Yahoo", []);
        var fallen = new Provider("Yahoo", [], answers: false);
        var coinGecko = new Provider("CoinGecko", []);

        Assert.True((await Dispatcher(covered, coinGecko).GetHistoryAsync(Request())).Answered);
        Assert.False((await Dispatcher(fallen, coinGecko).GetHistoryAsync(Request())).Answered);
    }

    private static PriceHistoryDispatcher Dispatcher(params IPriceHistoryProvider[] providers) =>
        new(providers, NullLogger<PriceHistoryDispatcher>.Instance);

    private static PriceHistoryRequest Request() =>
        new(Asset, "BTC", AssetClass.Crypto, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3));

    private static int[] Days(params int[] days) => days;

    private sealed class Provider(string name, int[] days, bool answers = true) : IPriceHistoryProvider
    {
        public string Name => name;

        internal List<PriceHistoryRequest> Asked { get; } = [];

        public Task<PriceHistoryResult> GetHistoryAsync(
            PriceHistoryRequest request,
            CancellationToken cancellationToken = default)
        {
            Asked.Add(request);

            if (!answers)
            {
                return Task.FromResult(PriceHistoryResult.Failed);
            }

            var prices = days
                .Select(day => new DateOnly(2026, 3, day))
                .Where(date => date >= request.From && date <= request.To)
                .Select(date => new DailyPrice(Asset, date, 100m, name))
                .ToList();

            return Task.FromResult(PriceHistoryResult.Of(prices));
        }
    }
}
