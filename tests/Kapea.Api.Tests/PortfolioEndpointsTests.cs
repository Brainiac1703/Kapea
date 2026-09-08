using System.Net;
using System.Net.Http.Json;
using Kapea.Domain.Accounts;
using Kapea.Domain.ValueObjects;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class PortfolioEndpointsTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task A_request_without_a_token_does_not_reach_the_data()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_account_can_be_created_and_read_back()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var created = await client.PostAsJsonAsync(
            "/api/accounts", new CreateAccountRequest("Kraken", "Kraken principal", "EUR"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var accounts = await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts");

        Assert.Equal("Kraken principal", Assert.Single(accounts!).Alias);
    }

    [Fact]
    public async Task An_unsupported_platform_is_rejected_naming_the_supported_ones()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/accounts", new CreateAccountRequest("Binance", "Binance", "EUR"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Kraken", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_user_never_sees_the_accounts_of_another()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        await factory.CreateClientFor(theirs).PostAsJsonAsync(
            "/api/accounts", new CreateAccountRequest("Kraken", "Cuenta ajena", "EUR"));

        var accounts = await factory.CreateClientFor(mine)
            .GetFromJsonAsync<List<AccountResponse>>("/api/accounts");

        Assert.DoesNotContain(accounts!, account => account.Alias == "Cuenta ajena");
    }

    [Fact]
    public async Task The_user_identifier_is_never_taken_from_the_request()
    {
        // Aunque se intente colar otro identificador por parámetro, el usuario efectivo
        // sigue siendo el del token.
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        await factory.CreateClientFor(theirs).PostAsJsonAsync(
            "/api/accounts", new CreateAccountRequest("Bit2Me", "Cuenta ajena 2", "EUR"));

        var accounts = await factory.CreateClientFor(mine)
            .GetFromJsonAsync<List<AccountResponse>>($"/api/accounts?userId={theirs}");

        Assert.DoesNotContain(accounts!, account => account.Alias == "Cuenta ajena 2");
    }

    [Fact]
    public async Task Deleting_an_account_with_movements_is_refused()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var created = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await SeedTransactionAsync(user, created!.Id);

        var response = await client.DeleteAsync($"/api/accounts/{created.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("movimientos", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_account_can_be_deleted()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var created = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Vacía", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/accounts/{created!.Id}")).StatusCode);
    }

    [Fact]
    public async Task The_portfolio_says_when_its_figures_are_incomplete()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var created = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Con pendientes", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await SeedTransactionAsync(user, created!.Id, Domain.Transactions.TransactionType.Unknown);

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");

        Assert.False(portfolio!.IsComplete);
        Assert.True(portfolio.UnclassifiedTransactionCount > 0);
    }

    [Fact]
    public async Task The_results_of_a_year_with_nothing_come_back_empty_rather_than_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var results = await client.GetFromJsonAsync<TaxYearResultsResponse>("/api/results/2019");

        Assert.Equal(2019, results!.TaxYear);
        Assert.Empty(results.ByAsset);
        Assert.Equal(0m, results.TotalResultInEuros);
    }

    [Fact]
    public async Task A_result_that_does_not_exist_is_reported_as_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.GetAsync($"/api/results/detail/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_a_file_to_an_account_that_does_not_exist_is_reported_as_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        using var content = new MultipartFormDataContent
        {
            { new StringContent("ID;Type;Time;Amount"), "file", "export.csv" },
        };

        var response = await client.PostAsync($"/api/imports/file?accountId={Guid.NewGuid()}", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_an_xtb_export_returns_a_preview_without_persisting_anything()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB vista previa", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var preview = await UploadAsync(client, account!.Id, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            1002;Stocks purchase;11.01.2024 09:30:00;SAN.ES;;-1.000,00;EUR
            """);

        Assert.Equal("Staged", preview!.Run.Status);
        Assert.Equal(2, preview.Run.RecordsImported);

        await using var context = factory.CreateContext(user);

        Assert.Equal(0, await context.Transactions.CountAsync(t => t.AccountId == account.Id));
    }

    [Fact]
    public async Task Confirming_the_preview_persists_the_movements()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB confirmada", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var preview = await UploadAsync(client, account!.Id, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            """);

        var confirmed = await (await client.PostAsync($"/api/imports/{preview!.Run.Id}/confirm", null))
            .Content.ReadFromJsonAsync<ImportRunResponse>();

        Assert.Equal("Completed", confirmed!.Status);

        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?accountId={account.Id}");

        var transaction = Assert.Single(transactions!);

        Assert.Equal("Deposit", transaction.Type);
        Assert.Equal(500m, transaction.GrossAmount);
        Assert.Equal(preview.Run.Id, transaction.ImportRunId);
        Assert.NotNull(transaction.RawContent);
    }

    [Fact]
    public async Task Discarding_the_preview_leaves_nothing_behind()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB descartada", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var preview = await UploadAsync(client, account!.Id, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            """);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync($"/api/imports/{preview!.Run.Id}/discard", null)).StatusCode);

        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>(
            $"/api/transactions?accountId={account.Id}");

        Assert.Empty(transactions!);
    }

    [Fact]
    public async Task An_unreadable_file_is_rejected_naming_what_was_expected()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB formato raro", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        using var content = new MultipartFormDataContent
        {
            { new StringContent("Fecha;Concepto;Saldo\n10.01.2024;Algo;100,00"), "file", "export.csv" },
        };

        var response = await client.PostAsync($"/api/imports/file?accountId={account!.Id}", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Columnas esperadas", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_of_an_unsupported_type_is_refused()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB pdf", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        using var content = new MultipartFormDataContent
        {
            { new StringContent("%PDF-1.4"), "file", "extracto.pdf" },
        };

        var response = await client.PostAsync($"/api/imports/file?accountId={account!.Id}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(".xlsx", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uploading_to_an_api_only_platform_is_refused()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Kraken por fichero", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        using var content = new MultipartFormDataContent
        {
            { new StringContent("ID;Type;Time;Amount"), "file", "export.csv" },
        };

        var response = await client.PostAsync($"/api/imports/file?accountId={account!.Id}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Xtb", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reimporting_the_same_file_discards_everything_as_duplicates()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB reimportada", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        const string csv = """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            """;

        var first = await UploadAsync(client, account!.Id, csv);
        await client.PostAsync($"/api/imports/{first!.Run.Id}/confirm", null);

        var second = await UploadAsync(client, account.Id, csv);

        Assert.Equal(0, second!.Run.RecordsImported);
        Assert.Equal(1, second.Run.DuplicatesDiscarded);
    }

    [Fact]
    public async Task The_history_of_imports_lists_the_runs_with_their_counts()
    {
        var user = Guid.NewGuid();
        var client = factory.CreateClientFor(user);

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB historial", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await UploadAsync(client, account!.Id, """
            ID;Type;Time;Symbol;Comment;Amount;Currency
            1001;Deposit;10.01.2024 09:30:00;;;500,00;EUR
            1002;fecha rota;;;;;
            """);

        var runs = await client.GetFromJsonAsync<List<ImportRunResponse>>($"/api/imports?accountId={account.Id}");
        var run = Assert.Single(runs!);

        Assert.Equal(1, run.RecordsImported);
        Assert.Equal(1, run.RecordsRejected);
        Assert.Contains(run.Rejected, rejected => rejected.Reason.Length > 0);
    }

    private static async Task<ImportPreviewResponse?> UploadAsync(HttpClient client, Guid accountId, string csv)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(csv), "file", "export.csv" },
        };

        var response = await client.PostAsync($"/api/imports/file?accountId={accountId}", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ImportPreviewResponse>();
    }

    private async Task SeedTransactionAsync(
        Guid user,
        Guid accountId,
        Domain.Transactions.TransactionType type = Domain.Transactions.TransactionType.Deposit)
    {
        await using var context = factory.CreateContext(user);

        context.Transactions.Add(Domain.Transactions.Transaction.Imported(
            new UserId(user),
            accountId,
            type,
            null,
            Quantity.Zero,
            null,
            Money.Euros(100m),
            Money.Euros(0m),
            Domain.Transactions.Occurrence.FromOffset(DateTimeOffset.UtcNow, "UTC"),
            Domain.Transactions.TransactionSource.FromImport(
                Guid.NewGuid(), Guid.NewGuid().ToString(), null, Guid.NewGuid().ToString())));

        await context.SaveChangesAsync();
    }
}
