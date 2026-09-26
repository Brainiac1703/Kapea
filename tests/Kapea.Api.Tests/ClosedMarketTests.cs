using System.Net.Http.Json;
using Kapea.Domain.Assets;
using Kapea.Domain.MarketData;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

/// <summary>
/// El mercado cerrado, de extremo a extremo.
/// </summary>
/// <remarks>
/// Lo que el usuario veía: la gráfica del patrimonio cortada todos los fines de semana
/// desde que tiene acciones, y un desplome de la renta variable en los días cortados.
/// </remarks>
[Collection(ApiCollection.Name)]
public class ClosedMarketTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task A_day_without_a_quote_says_whether_the_market_was_closed()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        // Dos acciones que cotizan jueves, viernes y lunes. Sábado y domingo, ninguna.
        var first = await QuotedAsync(user, AssetClass.Equity, [2, 3, 6]);
        await QuotedAsync(user, AssetClass.Equity, [2, 3, 6]);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{first}?from=2026-07-02&to=2026-07-06");

        var saturday = history!.Days.Single(day => day.Date == new DateOnly(2026, 7, 4));

        Assert.True(saturday.MarketClosed);
        Assert.Null(saturday.PriceInEuros);
        Assert.False(history.Days.Single(day => day.Date == new DateOnly(2026, 7, 2)).MarketClosed);
    }

    [Fact]
    public async Task A_gap_that_is_not_a_closure_is_not_reported_as_one()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        // A ésta le falta el día 3; la otra sí lo tiene, así que la bolsa abrió.
        var first = await QuotedAsync(user, AssetClass.Equity, [2, 6]);
        await QuotedAsync(user, AssetClass.Equity, [2, 3, 6]);

        var history = await client.GetFromJsonAsync<AssetHistoryResponse>(
            $"/api/portfolio/history/{first}?from=2026-07-02&to=2026-07-06");

        Assert.False(history!.Days.Single(day => day.Date == new DateOnly(2026, 7, 3)).MarketClosed);
    }

    [Fact]
    public async Task The_full_range_of_the_portfolio_starts_at_the_first_movement()
    {
        // Antes devolvía diez años, de los que sólo los últimos tenían valor, y la línea
        // real quedaba aplastada contra el borde.
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Con historia", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await SeedMovementAsync(user, account!.Id, new DateOnly(2026, 7, 2));

        var history = await client.GetFromJsonAsync<PortfolioHistoryResponse>("/api/portfolio/history?all=true");

        Assert.Equal(new DateOnly(2026, 7, 2), history!.Days[0].Date);
    }

    [Fact]
    public async Task Without_any_movement_the_full_range_is_not_years_of_nothing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var history = await client.GetFromJsonAsync<PortfolioHistoryResponse>("/api/portfolio/history?all=true");

        Assert.Single(history!.Days);
    }

    private async Task<Guid> QuotedAsync(Guid user, AssetClass assetClass, int[] days)
    {
        await using var context = factory.CreateContext(user);

        var asset = Asset.Create($"Q{Guid.NewGuid():N}"[..10].ToUpperInvariant(), assetClass);
        context.Assets.Add(asset);

        foreach (var day in days)
        {
            context.DailyPrices.Add(new DailyPrice(asset.Id, new DateOnly(2026, 7, day), 100m + day, "Prueba"));
        }

        await context.SaveChangesAsync();

        return asset.Id;
    }

    private async Task SeedMovementAsync(Guid user, Guid accountId, DateOnly on)
    {
        await using var context = factory.CreateContext(user);

        context.Transactions.Add(Domain.Transactions.Transaction.Imported(
            new Domain.ValueObjects.UserId(user),
            accountId,
            Domain.Transactions.TransactionType.Deposit,
            null,
            Domain.ValueObjects.Quantity.Zero,
            null,
            Domain.ValueObjects.Money.Euros(100m),
            Domain.ValueObjects.Money.Euros(0m),
            Domain.Transactions.Occurrence.FromOffset(
                new DateTimeOffset(on, TimeOnly.MinValue, TimeSpan.Zero), "UTC"),
            Domain.Transactions.TransactionSource.FromImport(
                Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString())));

        await context.SaveChangesAsync();
    }
}
