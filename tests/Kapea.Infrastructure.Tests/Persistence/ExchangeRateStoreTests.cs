using Kapea.Domain.Exchange;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.ExchangeRates;
using Kapea.Infrastructure.Persistence.Stores;
using Kapea.Infrastructure.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class ExchangeRateStoreTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_rate_is_read_back_by_currency_and_date()
    {
        var currency = await SeedAsync(new DateOnly(2025, 3, 10), 1.0838m);

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));
        var store = new ExchangeRateStore(context);

        var rates = await store.GetOnOrBeforeAsync(currency, new DateOnly(2025, 3, 10), 10);

        Assert.Equal(1.0838m, Assert.Single(rates).UnitsPerEuro);
    }

    [Fact]
    public async Task Rates_come_back_newest_first_and_only_within_the_lookback()
    {
        var currency = NewCurrency();

        await UpsertAsync(
            new DailyRate(currency, new DateOnly(2025, 3, 10), 1.08m),
            new DailyRate(currency, new DateOnly(2025, 3, 14), 1.09m),
            new DailyRate(currency, new DateOnly(2025, 1, 2), 1.05m));

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));

        var rates = await new ExchangeRateStore(context)
            .GetOnOrBeforeAsync(currency, new DateOnly(2025, 3, 15), maximumLookbackDays: 10);

        Assert.Equal([new DateOnly(2025, 3, 14), new DateOnly(2025, 3, 10)], rates.Select(rate => rate.Date));
    }

    [Fact]
    public async Task Reingesting_the_same_range_does_not_duplicate()
    {
        var currency = NewCurrency();
        var batch = new[]
        {
            new DailyRate(currency, new DateOnly(2025, 3, 10), 1.08m),
            new DailyRate(currency, new DateOnly(2025, 3, 14), 1.09m),
        };

        var first = await UpsertAsync(batch);
        var second = await UpsertAsync(batch);

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));
        var stored = await new ExchangeRateStore(context).GetOnOrBeforeAsync(currency, new DateOnly(2025, 3, 31), 60);

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, stored.Count);
    }

    [Fact]
    public async Task A_revised_rate_updates_the_stored_one_instead_of_adding_another()
    {
        var currency = NewCurrency();

        await UpsertAsync(new DailyRate(currency, new DateOnly(2025, 3, 10), 1.08m));
        var written = await UpsertAsync(new DailyRate(currency, new DateOnly(2025, 3, 10), 1.11m));

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));
        var stored = await new ExchangeRateStore(context).GetOnOrBeforeAsync(currency, new DateOnly(2025, 3, 10), 10);

        Assert.Equal(1, written);
        Assert.Equal(1.11m, Assert.Single(stored).UnitsPerEuro);
    }

    [Fact]
    public async Task A_downloaded_range_is_ingested_complete()
    {
        // Se ingesta el fichero grabado del BCE y se comprueba que el almacén sirve
        // después lo que la fuente traía, sin pasar por la red en la lectura.
        var handler = new RecordedResponseHandler()
            .RespondWithFile(Path.Combine("ExchangeRates", "Recorded", "eurofxref-hist-sample.xml"));

        var source = new EcbExchangeRateSource(
            new HttpClient(handler) { BaseAddress = new Uri("https://www.ecb.europa.eu/") },
            NullLogger<EcbExchangeRateSource>.Instance);

        var downloaded = await source.FetchAsync(new DateOnly(2025, 3, 1), new DateOnly(2025, 4, 30));
        await UpsertAsync([.. downloaded]);

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));
        var provider = new StoredExchangeRateProvider(new ExchangeRateStore(context));

        var resolved = await provider.ResolveAsync(Currency.FromCode("USD"), new DateOnly(2025, 3, 15));

        Assert.NotNull(resolved);
        Assert.Equal(1.0882m, resolved!.UnitsPerEuro);
        Assert.True(resolved.WasSubstituted);
    }

    [Fact]
    public async Task The_euro_needs_no_rate_against_itself()
    {
        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));

        Assert.Null(await new StoredExchangeRateProvider(new ExchangeRateStore(context))
            .ResolveAsync(Currency.Euro, new DateOnly(2025, 3, 10)));
    }

    [Fact]
    public async Task A_gap_wider_than_the_lookback_resolves_to_nothing()
    {
        // Un hueco de meses no es un festivo: es un fallo de ingesta, y conviene que
        // salte en lugar de aplicar en silencio un tipo de hace medio año.
        var currency = await SeedAsync(new DateOnly(2025, 1, 2), 1.05m);

        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));

        Assert.Null(await new StoredExchangeRateProvider(new ExchangeRateStore(context))
            .ResolveAsync(currency, new DateOnly(2025, 6, 1)));
    }

    private async Task<Currency> SeedAsync(DateOnly date, decimal unitsPerEuro)
    {
        var currency = NewCurrency();

        await UpsertAsync(new DailyRate(currency, date, unitsPerEuro));

        return currency;
    }

    private async Task<int> UpsertAsync(params DailyRate[] rates)
    {
        await using var context = fixture.CreateContext(new UserId(Guid.NewGuid()));

        return await new ExchangeRateStore(context).UpsertAsync(rates);
    }

    /// <summary>
    /// Cada test usa una divisa inventada para no pisarse con los demás: la tabla de
    /// tipos es un catálogo global y la comparten todas las pruebas de la colección.
    /// </summary>
    private static Currency NewCurrency() =>
        Currency.FromCode(string.Create(3, Random.Shared.Next(0, 26 * 26 * 26), (span, seed) =>
        {
            span[0] = (char)('A' + (seed / 676 % 26));
            span[1] = (char)('A' + (seed / 26 % 26));
            span[2] = (char)('A' + (seed % 26));
        }));
}
