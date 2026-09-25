using System.Net;
using System.Net.Http.Json;
using Kapea.Domain.ValueObjects;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class WatchlistEndpointsTests(KapeaApiFactory factory)
{
    private static readonly DateTimeOffset Day = new(2025, 6, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_asset_can_be_watched_before_ever_owning_it()
    {
        // Es el motivo del cambio: un sistema de entrada existe para decir dónde entrar,
        // y sólo podía aplicarse a lo que ya se había comprado.
        var client = factory.CreateClientFor(Guid.NewGuid());
        var symbol = "WCH" + Suffix();

        var response = await client.PostAsJsonAsync("/api/watchlist", new WatchAssetRequest(symbol, "Crypto"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var added = (await response.Content.ReadFromJsonAsync<WatchAssetResponse>())!;

        Assert.Equal(symbol, added.Asset.Symbol);
        Assert.False(added.Asset.IsHeld);
        Assert.False(added.AlreadyWatched);

        Assert.Contains(await ListAsync(client), watched => watched.Symbol == symbol);
    }

    [Fact]
    public async Task Watching_the_same_asset_twice_does_not_duplicate_it()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var symbol = "TWI" + Suffix();

        await client.PostAsJsonAsync("/api/watchlist", new WatchAssetRequest(symbol, "Crypto"));
        var second = await client.PostAsJsonAsync("/api/watchlist", new WatchAssetRequest(symbol, "Crypto"));

        Assert.True((await second.Content.ReadFromJsonAsync<WatchAssetResponse>())!.AlreadyWatched);
        Assert.Single(await ListAsync(client), watched => watched.Symbol == symbol);
    }

    [Fact]
    public async Task What_you_own_is_watched_without_adding_it()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var symbol = "OWN" + Suffix();

        await SeedPositionAsync(user, client, symbol);

        var watched = Assert.Single(await ListAsync(client), entry => entry.Symbol == symbol);

        Assert.True(watched.IsHeld);
        Assert.Equal(2m, watched.Quantity);
    }

    [Fact]
    public async Task An_asset_you_own_cannot_be_dropped()
    {
        // Dejaría una posición de la cartera sin precio, sin señales y sin evolución.
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var symbol = "HLD" + Suffix();

        await SeedPositionAsync(user, client, symbol);
        var watched = Assert.Single(await ListAsync(client), entry => entry.Symbol == symbol);

        var response = await client.DeleteAsync($"/api/watchlist/{watched.AssetId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("cartera", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dropping_something_you_only_watched_keeps_the_asset_and_its_prices()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var symbol = "DRP" + Suffix();

        var added = (await (await client.PostAsJsonAsync(
                "/api/watchlist", new WatchAssetRequest(symbol, "Crypto")))
            .Content.ReadFromJsonAsync<WatchAssetResponse>())!;

        var response = await client.DeleteAsync($"/api/watchlist/{added.Asset.AssetId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await ListAsync(client), watched => watched.Symbol == symbol);

        // El activo sigue en el catálogo: deja de mirarse, no deja de existir.
        await using var context = factory.CreateContext(Guid.NewGuid());
        Assert.Contains(context.Assets, asset => asset.CanonicalSymbol == symbol);
    }

    [Fact]
    public async Task Selling_everything_keeps_the_asset_on_the_list()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var symbol = "SLD" + Suffix();

        await SeedPositionAsync(user, client, symbol, sellEverything: true);

        var watched = Assert.Single(await ListAsync(client), entry => entry.Symbol == symbol);

        Assert.False(watched.IsHeld);
        Assert.Null(watched.Quantity);
    }

    [Fact]
    public async Task One_person_never_sees_what_another_watches()
    {
        var mine = factory.CreateClientFor(Guid.NewGuid());
        var theirs = factory.CreateClientFor(Guid.NewGuid());
        var symbol = "PRV" + Suffix();

        await theirs.PostAsJsonAsync("/api/watchlist", new WatchAssetRequest(symbol, "Crypto"));

        Assert.DoesNotContain(await ListAsync(mine), watched => watched.Symbol == symbol);
    }

    [Fact]
    public async Task Writing_down_an_idea_starts_following_its_asset()
    {
        // Una idea sobre algo que no se vigila no sirve: sin precios no hay forma de
        // comprobar si acertó, y seguir su resultado es lo que la pantalla promete.
        var client = factory.CreateClientFor(Guid.NewGuid());
        var symbol = "IDE" + Suffix();

        var source = await (await client.PostAsJsonAsync(
                "/api/ideas/sources", new CreateIdeaSourceRequest("Un boletín", null)))
            .Content.ReadFromJsonAsync<IdeaSourceResponse>();

        var response = await client.PostAsJsonAsync("/api/ideas", new CreateIdeaRequest(
            source!.Id, symbol, "Buy", new DateOnly(2026, 5, 1), null, null, null, null, null, "Crypto"));

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Contains(await ListAsync(client), watched => watched.Symbol == symbol);
    }

    private static async Task<IReadOnlyList<WatchedAssetResponse>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<WatchedAssetResponse>>("/api/watchlist"))!;

    private static string Suffix() =>
        Random.Shared.Next(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Una compra importada y, si se pide, su venta completa después.</summary>
    private async Task SeedPositionAsync(Guid user, HttpClient client, string symbol, bool sellEverything = false)
    {
        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Seguimiento " + symbol, "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await using (var context = factory.CreateContext(user))
        {
            var asset = Domain.Assets.Asset.Create(symbol, Domain.Assets.AssetClass.Crypto);
            context.Assets.Add(asset);

            context.Transactions.Add(Domain.Transactions.Transaction.Imported(
                new UserId(user), account!.Id, Domain.Transactions.TransactionType.Buy, asset.Id, new Quantity(2m),
                Money.Euros(10m), Money.Euros(20m), Money.Euros(0m),
                Domain.Transactions.Occurrence.FromOffset(Day, "UTC"),
                Domain.Transactions.TransactionSource.FromImport(
                    Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString())));

            if (sellEverything)
            {
                context.Transactions.Add(Domain.Transactions.Transaction.Imported(
                    new UserId(user), account.Id, Domain.Transactions.TransactionType.Sell, asset.Id, new Quantity(2m),
                    Money.Euros(12m), Money.Euros(24m), Money.Euros(0m),
                    Domain.Transactions.Occurrence.FromOffset(Day.AddMonths(1), "UTC"),
                    Domain.Transactions.TransactionSource.FromImport(
                        Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString())));
            }

            context.WatchedAssets.Add(
                Domain.Assets.WatchedAsset.Of(new UserId(user), asset.Id, Day));

            await context.SaveChangesAsync();
        }

        await client.PostAsync("/api/portfolio/recalculate", null);
    }
}
