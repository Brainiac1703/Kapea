using System.Net;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.ExchangeRates;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.ExchangeRates;

public class EcbExchangeRateSourceTests
{
    [Fact]
    public async Task A_date_range_is_ingested_complete()
    {
        var (source, handler) = Create();

        var rates = await source.FetchAsync(new DateOnly(2025, 3, 1), new DateOnly(2025, 4, 30));

        Assert.Equal(8, rates.Count);
        Assert.Equal(3, rates.Select(rate => rate.Date).Distinct().Count());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Dates_outside_the_requested_range_are_left_out()
    {
        var (source, _) = Create();

        var rates = await source.FetchAsync(new DateOnly(2025, 3, 12), new DateOnly(2025, 3, 20));

        Assert.All(rates, rate => Assert.Equal(new DateOnly(2025, 3, 14), rate.Date));
        Assert.Equal(1.0882m, rates.Single(rate => rate.Currency == Currency.FromCode("USD")).UnitsPerEuro);
    }

    [Fact]
    public async Task A_failing_response_surfaces_as_an_error()
    {
        var handler = new RecordedResponseHandler().RespondWithStatus(HttpStatusCode.ServiceUnavailable);
        var source = new EcbExchangeRateSource(Client(handler), NullLogger<EcbExchangeRateSource>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => source.FetchAsync(new DateOnly(2025, 3, 1), new DateOnly(2025, 3, 31)));
    }

    [Fact]
    public async Task An_inverted_range_is_rejected()
    {
        var (source, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(
            () => source.FetchAsync(new DateOnly(2025, 4, 1), new DateOnly(2025, 3, 1)));
    }

    [Theory]
    [InlineData(0, EcbExchangeRateSource.DailyPath)]
    [InlineData(30, EcbExchangeRateSource.NinetyDayPath)]
    [InlineData(400, EcbExchangeRateSource.HistoryPath)]
    public void The_smallest_file_covering_the_range_is_chosen(int daysAgo, string expectedPath)
    {
        var today = new DateOnly(2026, 9, 8);

        Assert.Equal(expectedPath, EcbExchangeRateSource.ChoosePath(today.AddDays(-daysAgo), today));
    }

    private static (EcbExchangeRateSource Source, RecordedResponseHandler Handler) Create()
    {
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Path.Combine("ExchangeRates", "Recorded", "eurofxref-hist-sample.xml"));

        return (new EcbExchangeRateSource(Client(handler), NullLogger<EcbExchangeRateSource>.Instance), handler);
    }

    private static HttpClient Client(RecordedResponseHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://www.ecb.europa.eu/") };
}
