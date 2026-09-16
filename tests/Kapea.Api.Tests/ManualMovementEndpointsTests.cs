using System.Net;
using System.Net.Http.Json;
using Kapea.Domain.ValueObjects;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class ManualMovementEndpointsTests(KapeaApiFactory factory)
{
    private static readonly DateTimeOffset Day = new(2024, 1, 10, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_manual_buy_enters_the_position_after_recalculating()
    {
        var (client, account) = await AccountAsync("Kraken", "Manual en posición");

        await RegisterAsync(client, Buy(account, "MANU" + Suffix(), quantity: 2m, price: 10m));

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");
        var position = portfolio!.Groups.SelectMany(group => group.Positions).Single(p => p.AssetSymbol.StartsWith("MANU", StringComparison.Ordinal));

        Assert.Equal(2m, position.Quantity);
        Assert.Equal(20m, position.CostInEuros);
    }

    [Fact]
    public async Task A_manual_movement_shows_its_origin_note_and_moment_in_the_list()
    {
        var (client, account) = await AccountAsync("Xtb", "Manual en lista");

        await RegisterAsync(client, Buy(account, "LIST" + Suffix(), text: "compra en papel"));

        var page = await SearchAsync(client, account, "origin=Manual");
        var movement = Assert.Single(page.Items);

        Assert.Equal(MovementOrigins.Manual, movement.Origin);
        Assert.Equal("compra en papel", movement.Note);
        Assert.NotNull(movement.RegisteredAt);
        Assert.Null(movement.RevisedAt);
    }

    [Fact]
    public async Task Editing_a_manual_movement_changes_the_figures_and_records_when()
    {
        var (client, account) = await AccountAsync("Kraken", "Manual editado");
        var symbol = "EDIT" + Suffix();
        var id = await RegisterAsync(client, Buy(account, symbol, quantity: 2m));

        var response = await client.PutAsJsonAsync($"/api/transactions/{id}", Buy(account, symbol, quantity: 5m));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var movement = Assert.Single((await SearchAsync(client, account)).Items);
        Assert.Equal(5m, movement.Quantity);
        Assert.NotNull(movement.RevisedAt);
    }

    [Fact]
    public async Task Deleting_a_manual_movement_removes_it()
    {
        var (client, account) = await AccountAsync("Kraken", "Manual borrado");
        var id = await RegisterAsync(client, Buy(account, "DEL" + Suffix()));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/{id}")).StatusCode);
        Assert.Empty((await SearchAsync(client, account)).Items);
    }

    [Fact]
    public async Task An_unknown_type_is_rejected_with_its_name()
    {
        var (client, account) = await AccountAsync("Kraken", "Tipo inventado");

        var response = await client.PostAsJsonAsync("/api/transactions", Buy(account, "X") with { Type = "Regalo" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Regalo", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Someone_else_cannot_touch_my_account_nor_my_movements()
    {
        var (mine, account) = await AccountAsync("Kraken", "Ajena");
        var id = await RegisterAsync(mine, Buy(account, "OWN" + Suffix()));

        var stranger = factory.CreateClientFor(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync("/api/transactions", Buy(account, "X"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/transactions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/transactions/{id}/void", new VoidMovementRequest("x"))).StatusCode);
    }

    [Fact]
    public async Task Voiding_an_imported_movement_and_importing_again_does_not_bring_it_back()
    {
        var (client, account) = await AccountAsync("Xtb", "Anulado y reimportado");
        const string csv = """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            5001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            """;

        var first = await UploadAsync(client, account, csv);
        await client.PostAsync($"/api/imports/{first.Run.Id}/confirm", null);

        var imported = Assert.Single((await SearchAsync(client, account)).Items);
        Assert.Equal(MovementOrigins.File, imported.Origin);

        var voided = await client.PostAsJsonAsync($"/api/transactions/{imported.Id}/void", new VoidMovementRequest("duplicado"));
        Assert.Equal(HttpStatusCode.NoContent, voided.StatusCode);

        var second = await UploadAsync(client, account, csv);

        Assert.Equal(0, second.Run.RecordsImported);
        Assert.Equal(1, second.Run.DuplicatesDiscarded);

        var stillVoided = Assert.Single((await SearchAsync(client, account, "voided=true")).Items);
        Assert.Equal("duplicado", stillVoided.VoidReason);
        Assert.Empty((await SearchAsync(client, account, "voided=false")).Items);

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");
        Assert.DoesNotContain(portfolio!.Cash, cash => cash.AccountId == account);
    }

    [Fact]
    public async Task Restoring_brings_the_movement_back_into_the_figures()
    {
        var (client, account) = await AccountAsync("Xtb", "Restaurado");
        var preview = await UploadAsync(client, account, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            5101;Deposit;10.01.2024 09:30:00;;;250,00;EUR
            """);
        await client.PostAsync($"/api/imports/{preview.Run.Id}/confirm", null);
        var imported = Assert.Single((await SearchAsync(client, account)).Items);

        await client.PostAsJsonAsync($"/api/transactions/{imported.Id}/void", new VoidMovementRequest("probando"));
        await client.PostAsync($"/api/transactions/{imported.Id}/restore", null);

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");
        Assert.Equal(250m, portfolio!.Cash.Single(cash => cash.AccountId == account).Amount);
    }

    [Fact]
    public async Task An_imported_movement_is_not_deleted_as_if_it_were_manual()
    {
        var (client, account) = await AccountAsync("Xtb", "Importado no se borra");
        var preview = await UploadAsync(client, account, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            5201;Deposit;10.01.2024 09:30:00;;;100,00;EUR
            """);
        await client.PostAsync($"/api/imports/{preview.Run.Id}/confirm", null);
        var imported = Assert.Single((await SearchAsync(client, account)).Items);

        var response = await client.DeleteAsync($"/api/transactions/{imported.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("se anula", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Correcting_voids_the_import_and_leaves_an_adjustment_with_its_reason()
    {
        var (client, account) = await AccountAsync("Xtb", "Corregido");
        var preview = await UploadAsync(client, account, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            5301;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            """);
        await client.PostAsync($"/api/imports/{preview.Run.Id}/confirm", null);
        var imported = Assert.Single((await SearchAsync(client, account)).Items);

        var corrected = new ManualMovementRequest(
            account, "Deposit", null, null, 0m, null, 450m, "EUR", 0m, Day, "Europe/Madrid", "el ingreso fue de 450");
        var response = await client.PostAsJsonAsync($"/api/transactions/{imported.Id}/correct", corrected);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var adjustment = Assert.Single((await SearchAsync(client, account, "origin=ManualAdjustment")).Items);
        Assert.Equal("el ingreso fue de 450", adjustment.AdjustmentReason);
        Assert.Single((await SearchAsync(client, account, "voided=true")).Items);

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");
        Assert.Equal(450m, portfolio!.Cash.Single(cash => cash.AccountId == account).Amount);
    }

    [Fact]
    public async Task A_manual_movement_already_in_the_file_is_flagged_in_the_preview_and_in_review()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var account = await CreateAccountAsync(client, "Xtb", "Coincidencia");

        var manualId = await RegisterAsync(client, new ManualMovementRequest(
            account, "Deposit", null, null, 0m, null, 500m, "EUR", 0m, Day, "Europe/Madrid", "ingreso apuntado"));

        var preview = await UploadAsync(client, account, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            5401;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            """);

        var match = Assert.Single(preview.Run.ManualMatches);
        Assert.Equal(manualId, match.ManualId);
        Assert.Equal("ingreso apuntado", match.ManualNote);

        await client.PostAsync($"/api/imports/{preview.Run.Id}/confirm", null);

        var pending = await client.GetFromJsonAsync<PendingReviewResponse>("/api/review/pending");
        Assert.Equal(1, pending!.ManualDuplicates);

        var pair = Assert.Single((await client.GetFromJsonAsync<List<ManualDuplicateResponse>>("/api/review/manual-duplicates"))!);
        Assert.Equal(manualId, pair.Manual.Id);

        var distinct = await client.PostAsync($"/api/transactions/{manualId}/distinct/{pair.Imported.Id}", null);
        Assert.Equal(HttpStatusCode.NoContent, distinct.StatusCode);

        Assert.Equal(0, (await client.GetFromJsonAsync<PendingReviewResponse>("/api/review/pending"))!.ManualDuplicates);
    }

    [Fact]
    public async Task Voiding_an_unclassified_movement_takes_it_out_of_pending()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var account = await CreateAccountAsync(client, "Kraken", "Sin clasificar anulado");

        Guid id;
        await using (var context = factory.CreateContext(user))
        {
            var unknown = Domain.Transactions.Transaction.Imported(
                new UserId(user), account, Domain.Transactions.TransactionType.Unknown, null, Quantity.Zero, null,
                Money.Euros(3m), Money.Euros(0m), Domain.Transactions.Occurrence.FromOffset(Day, "UTC"),
                Domain.Transactions.TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString()));
            context.Transactions.Add(unknown);
            await context.SaveChangesAsync();
            id = unknown.Id;
        }

        Assert.Equal(1, (await client.GetFromJsonAsync<PendingReviewResponse>("/api/review/pending"))!.Transactions);

        await client.PostAsJsonAsync($"/api/transactions/{id}/void", new VoidMovementRequest("ruido de la plataforma"));

        Assert.Equal(0, (await client.GetFromJsonAsync<PendingReviewResponse>("/api/review/pending"))!.Transactions);
        Assert.True((await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio"))!.UnclassifiedTransactionCount == 0);
    }

    [Fact]
    public async Task A_sale_that_consumes_a_manual_buy_says_so_in_the_tax_detail()
    {
        var (client, account) = await AccountAsync("Kraken", "Fiscal manual");
        var symbol = "TAX" + Suffix();

        await RegisterAsync(client, Buy(account, symbol, quantity: 2m, price: 10m));
        await RegisterAsync(client, Buy(account, symbol, quantity: 2m, price: 15m, day: Day.AddDays(30)) with { Type = "Sell" });

        var results = await client.GetFromJsonAsync<TaxYearResultsResponse>("/api/results/2024");
        var sale = results!.Details.Single(detail => detail.AssetSymbol == symbol);

        Assert.Equal(MovementOrigins.Manual, sale.DisposalOrigin);
        Assert.All(sale.ConsumedLots, lot => Assert.Equal(MovementOrigins.Manual, lot.AcquisitionOrigin));
    }

    [Fact]
    public async Task Deleting_the_only_movement_of_an_asset_removes_its_position()
    {
        // El recálculo sólo rehacía los activos que aún tenían movimientos: borrado el
        // último, la posición se quedaba guardada como si nada.
        var (client, account) = await AccountAsync("Kraken", "Único borrado");
        var symbol = "ONLY" + Suffix();
        var id = await RegisterAsync(client, Buy(account, symbol, quantity: 2m));

        await client.DeleteAsync($"/api/transactions/{id}");

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");
        Assert.DoesNotContain(portfolio!.Groups.SelectMany(group => group.Positions), position => position.AssetSymbol == symbol);
    }

    [Fact]
    public async Task Voiding_the_only_movement_of_an_asset_removes_its_position()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);
        var account = await CreateAccountAsync(client, "Kraken", "Único anulado");
        var symbol = "VOID" + Suffix();

        Guid id;
        await using (var context = factory.CreateContext(user))
        {
            var asset = Domain.Assets.Asset.Create(symbol, Domain.Assets.AssetClass.Crypto);
            context.Assets.Add(asset);

            var buy = Domain.Transactions.Transaction.Imported(
                new UserId(user), account, Domain.Transactions.TransactionType.Buy, asset.Id, new Quantity(1m),
                Money.Euros(10m), Money.Euros(10m), Money.Euros(0m), Domain.Transactions.Occurrence.FromOffset(Day, "UTC"),
                Domain.Transactions.TransactionSource.FromImport(Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString()));
            context.Transactions.Add(buy);
            await context.SaveChangesAsync();
            id = buy.Id;
        }

        await client.PostAsync("/api/portfolio/recalculate", null);
        Assert.Contains((await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio"))!.Groups.SelectMany(group => group.Positions), position => position.AssetSymbol == symbol);

        await client.PostAsJsonAsync($"/api/transactions/{id}/void", new VoidMovementRequest("no era mía"));

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");
        Assert.DoesNotContain(portfolio!.Groups.SelectMany(group => group.Positions), position => position.AssetSymbol == symbol);
    }

    [Fact]
    public async Task The_impact_names_the_oldest_year_involved()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var impact = await client.GetFromJsonAsync<CorrectionImpactResponse>("/api/transactions/impact?date=2026-01-05&previous=2023-04-01");

        Assert.Equal(2023, impact!.TaxYear);
        Assert.True(impact.IsPastYear);
    }

    private static string Suffix() => Random.Shared.Next(100000, 999999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static ManualMovementRequest Buy(
        Guid account, string symbol, decimal quantity = 1m, decimal price = 10m, string? text = null, DateTimeOffset? day = null) =>
        new(account, "Buy", symbol, "Crypto", quantity, price, quantity * price, "EUR", 0m, day ?? Day, "Europe/Madrid", text);

    private async Task<(HttpClient Client, Guid Account)> AccountAsync(string platform, string alias)
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        return (client, await CreateAccountAsync(client, platform, alias));
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string platform, string alias) =>
        (await (await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(platform, alias, "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>())!.Id;

    private static async Task<Guid> RegisterAsync(HttpClient client, ManualMovementRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/transactions", request);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private static async Task<TransactionPageResponse> SearchAsync(HttpClient client, Guid account, string filter = "") =>
        (await client.GetFromJsonAsync<TransactionPageResponse>($"/api/transactions/search?accountId={account}&{filter}"))!;

    private static async Task<ImportPreviewResponse> UploadAsync(HttpClient client, Guid accountId, string csv)
    {
        using var content = new MultipartFormDataContent { { new StringContent(csv), "file", "export.csv" } };
        var response = await client.PostAsync($"/api/imports/file?accountId={accountId}", content);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<ImportPreviewResponse>())!;
    }

    private sealed record Created(Guid Id);
}
