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
    public async Task Asking_for_a_synchronisation_never_touches_the_accounts_of_another_person()
    {
        // Se lanza a mano desde la aplicación, así que tiene que quedarse en lo propio:
        // pedirla no puede servir para mover los datos de otro.
        var (mine, _) = await factory.CreateSignedInClientAsync();
        var (theirs, _) = await factory.CreateSignedInClientAsync();

        await theirs.PostAsJsonAsync(
            "/api/accounts", new CreateAccountRequest("Kraken", "Kraken ajena", "EUR"));

        var response = await mine.PostAsync("/api/sync", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<SynchronizationResponse>();

        // Sin credenciales propias no hay nada que sincronizar, y desde luego no la
        // cuenta del otro.
        Assert.Equal(0, report!.Accounts);
    }

    [Fact]
    public async Task A_credential_cannot_be_registered_on_the_account_of_another_person()
    {
        // La cuenta ajena no existe para quien pregunta, así que la petición se responde
        // como inexistente en lugar de dejar escribir sobre ella.
        var (mine, _) = await factory.CreateSignedInClientAsync();
        var theirs = await (await mine.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Kraken", "Kraken de otro", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var (attacker, _) = await factory.CreateSignedInClientAsync();

        var response = await attacker.PostAsJsonAsync(
            "/api/credentials",
            new RegisterBrokerCredentialRequest(theirs!.Id, "Kraken", "Mía", "clave", "secreto"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_file_platform_given_a_row_becomes_available_without_touching_code()
    {
        // Es lo que persigue el cambio entero: un bróker que exporta un fichero se da
        // de alta como un dato, y desde ese momento se le puede abrir una cuenta.
        var client = factory.CreateClientFor(Guid.NewGuid());
        var code = $"P{Guid.NewGuid().ToString("N")[..8]}";

        var created = await client.PostAsJsonAsync(
            "/api/platforms", new CreatePlatformRequest(code, "Bróker de prueba", "File"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var account = await client.PostAsJsonAsync(
            "/api/accounts", new CreateAccountRequest(code, "Cuenta del bróker nuevo", "EUR"));

        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        Assert.Equal(code, (await account.Content.ReadFromJsonAsync<AccountResponse>())!.Platform);
    }

    [Fact]
    public async Task An_api_platform_cannot_be_given_a_row_because_it_needs_an_adapter()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/platforms", new CreatePlatformRequest("Binance", "Binance", "Api"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("adaptador", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_platforms_that_come_built_in_cannot_be_retired()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.DeleteAsync("/api/platforms/Xtb");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_catalogue_says_how_each_platform_is_imported()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var platforms = await client.GetFromJsonAsync<List<PlatformResponse>>("/api/platforms") ?? [];

        Assert.Equal("File", platforms.Single(platform => platform.Code == "Xtb").ImportKind);
        Assert.Equal("Api", platforms.Single(platform => platform.Code == "Kraken").ImportKind);
        Assert.Equal("Api", platforms.Single(platform => platform.Code == "Bit2Me").ImportKind);
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
        Assert.Contains("Columnas encontradas", body, StringComparison.Ordinal);
        Assert.Contains("Perfiles configurados", body, StringComparison.Ordinal);
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

        // Bit2Me sí admite fichero, porque trae un perfil de serie que sabe leer su
        // resumen de movimientos. Kraken no tiene ninguno, así que su fichero se rechaza
        // diciendo por qué.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ningún perfil", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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

    [Fact]
    public async Task The_preview_shows_the_first_rows_already_interpreted()
    {
        // Es la contrapartida de aplicar un perfil sin preguntar: un mapeo equivocado
        // produce cifras plausibles, y solo se ve mirando filas concretas.
        var client = factory.CreateClientFor(Guid.NewGuid());

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB muestra", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var preview = await UploadAsync(client, account!.Id, Sample("9001"));

        var row = Assert.Single(preview!.Run.Sample);

        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Dividend", row.Type);
        Assert.Equal("SAN.ES", row.AssetSymbol);
        Assert.Equal(45.20m, row.GrossAmount);
        Assert.Equal("EUR", row.Currency);
        Assert.Equal("Importable", row.Outcome);
    }

    [Fact]
    public async Task The_import_says_which_profile_and_version_read_it()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB trazabilidad", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var preview = await UploadAsync(client, account!.Id, Sample("9002"));

        Assert.Equal("XTB · Operaciones de efectivo", preview!.Run.ProfileName);
        Assert.NotNull(preview.Run.ProfileVersion);
    }

    [Fact]
    public async Task A_row_already_imported_is_shown_as_such_before_confirming()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB repetido", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        var csv = Sample("9003");

        var first = await UploadAsync(client, account!.Id, csv);
        await client.PostAsync($"/api/imports/{first!.Run.Id}/confirm", null);

        var second = await UploadAsync(client, account.Id, csv);

        Assert.Equal("Duplicate", Assert.Single(second!.Run.Sample).Outcome);
    }

    private static string Sample(string id) =>
        "ID;Type;Time;Symbol;Comment;Amount;Currency\n"
        + $"{id};Dividend;15.05.2024 00:00:00;SAN.ES;DIV;45,20;EUR";

    [Fact]
    public async Task Correcting_a_profile_creates_a_version_and_leaves_the_previous_one_reachable()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var profiles = await client.GetFromJsonAsync<List<ImportProfileResponse>>("/api/profiles") ?? [];
        var profile = profiles.Single(entry => entry.Name == "XTB · Operaciones de efectivo");
        var before = profile.Versions.Single(version => version.Number == profile.CurrentVersion);

        var corrected = before with
        {
            RecognizedHeaders = [.. before.RecognizedHeaders, "Comment"],
        };

        var response = await client.PostAsJsonAsync(
            $"/api/profiles/{profile.Id}/versions",
            new ReviseImportProfileRequest(null, Rules(corrected)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var revised = (await response.Content.ReadFromJsonAsync<ImportProfileResponse>())!;

        Assert.Equal(profile.CurrentVersion + 1, revised.CurrentVersion);
        Assert.Contains(revised.Versions, version => version.Number == before.Number);
        Assert.Contains("Comment", revised.Versions.Single(v => v.Number == revised.CurrentVersion).RecognizedHeaders);

        // La versión anterior sigue tal cual: los movimientos que se importaron con ella
        // apuntan ahí, y reescribirla cambiaría en silencio cómo se explican sus cifras.
        var kept = revised.Versions.Single(version => version.Number == before.Number);

        Assert.Equal(before.RecognizedHeaders, kept.RecognizedHeaders);
        Assert.DoesNotContain("Comment", kept.RecognizedHeaders);
    }

    [Fact]
    public async Task A_profile_that_comes_built_in_cannot_be_deleted()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var profiles = await client.GetFromJsonAsync<List<ImportProfileResponse>>("/api/profiles") ?? [];
        var profile = profiles.First(entry => entry.BuiltIn);

        var response = await client.DeleteAsync($"/api/profiles/{profile.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_profile_without_a_date_column_is_refused_saying_what_is_missing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/profiles",
            new CreateImportProfileRequest(
                "Xtb",
                "Sin fecha",
                new ImportProfileRulesRequest(
                    ";", "European", "Europe/Madrid", "EUR", "SingleMovement", "Column", null, false,
                    ["Importe"], ["dd/MM/yyyy"], [],
                    new Dictionary<string, string> { ["GrossAmount"] = "Importe" },
                    new Dictionary<string, string>())));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("la fecha", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static ImportProfileRulesRequest Rules(ImportProfileVersionResponse version) =>
        new(
            version.Delimiter,
            version.DecimalConvention,
            version.TimeZoneId,
            version.FixedCurrency,
            version.RowShape,
            version.AmountSource,
            version.FixedAssetClass,
            version.AmountIsAlwaysPositive,
            version.RecognizedHeaders,
            version.DateFormats,
            version.NonFinancialConcepts,
            version.Columns,
            version.Concepts);

    [Fact]
    public async Task Movements_come_by_pages_with_their_filters()
    {
        // Un histórico de cripto son miles de apuntes. Traerlos todos para enseñar veinte
        // deja la pantalla en blanco mientras llegan.
        var (client, userId) = await factory.CreateSignedInClientAsync();
        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB páginas", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        for (var i = 0; i < 3; i++)
        {
            await SeedTransactionAsync(userId, account!.Id);
        }

        var page = await client.GetFromJsonAsync<TransactionPageResponse>(
            $"/api/transactions/search?accountId={account!.Id}&pageSize=2&page=1");

        Assert.Equal(3, page!.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(1, page.Page);

        var second = await client.GetFromJsonAsync<TransactionPageResponse>(
            $"/api/transactions/search?accountId={account.Id}&pageSize=2&page=2");

        Assert.Single(second!.Items);

        // Los tipos y los años salen de todo el histórico y no de la página: un filtro
        // que solo ofreciera lo visible no llevaría a lo que no se ve.
        Assert.NotEmpty(page.Types);
        Assert.NotEmpty(page.Years);
    }

    [Fact]
    public async Task Searching_movements_never_reaches_the_ones_of_another_person()
    {
        var (mine, _) = await factory.CreateSignedInClientAsync();
        var (theirs, theirId) = await factory.CreateSignedInClientAsync();

        var account = await (await theirs.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", "XTB ajena", "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        await SeedTransactionAsync(theirId, account!.Id);

        var page = await mine.GetFromJsonAsync<TransactionPageResponse>(
            $"/api/transactions/search?accountId={account.Id}");

        Assert.Equal(0, page!.Total);
    }

    private static async Task<ImportPreviewResponse?> UploadAsync(HttpClient client, Guid accountId, string csv)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(csv), "file", "export.csv" },
        };

        var response = await client.PostAsync($"/api/imports/file?accountId={accountId}", content);

        // El cuerpo lleva el detalle del problema. Perderlo dejaría el fallo en un
        // número, y averiguar la causa exigiría adivinar.
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

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
