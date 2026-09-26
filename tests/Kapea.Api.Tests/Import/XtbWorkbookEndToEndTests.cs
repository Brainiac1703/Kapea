using System.Net.Http.Json;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Tests.Import;

/// <summary>
/// El informe de XTB entero, desde el fichero subido hasta las cifras.
/// </summary>
/// <remarks>
/// El resto de las pruebas de importación comprueban las piezas: que se reconoce el
/// formato, que se leen los decimales, que un duplicado se descarta. Esta comprueba lo
/// único que el usuario mira, que es si el número que sale es el suyo, y lo hace por el
/// mismo camino que la aplicación: subir el libro, confirmarlo y consultar la cartera.
///
/// El contraste con el informe real dio la cifra exacta que declaraba el bróker. Esta
/// prueba deja fijado ese recorrido con datos inventados, para que un cambio que lo rompa
/// se note aquí y no en la siguiente declaración.
/// </remarks>
[Collection(ApiCollection.Name)]
public class XtbWorkbookEndToEndTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task The_whole_report_becomes_the_figures_the_broker_shows()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var account = await AccountAsync(client, "XTB informe completo");

        var preview = await UploadAsync(client, account);

        // Dos posiciones cerradas dan cuatro movimientos, y de la hoja de efectivo sólo
        // entra lo que ninguna otra trae: el ingreso, el interés, su retención y la
        // comisión de mercado. Las cuatro patas de compraventa que repite se descartan.
        Assert.Equal(8, preview.Run.RecordsImported);

        await ConfirmAsync(client, preview);

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");

        Assert.Equal(FakeXtbWorkbook.ExpectedRealized, portfolio!.RealizedResultInEuros);
        Assert.Equal(FakeXtbWorkbook.ExpectedCash, portfolio.CashTotalInEuros);

        // Todo vendido: ni posiciones abiertas ni precios que falten.
        Assert.Empty(portfolio.Groups);
        Assert.False(portfolio.MissingPrices);
        Assert.Equal(0, portfolio.UnclassifiedTransactionCount);
        Assert.Empty(portfolio.Inconsistencies);
    }

    [Fact]
    public async Task The_result_of_the_year_matches_the_report_asset_by_asset()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var account = await AccountAsync(client, "XTB resultado fiscal");

        await ConfirmAsync(client, await UploadAsync(client, account));

        var results = await client.GetFromJsonAsync<TaxYearResultsResponse>("/api/results/2026");

        Assert.Equal(FakeXtbWorkbook.ExpectedRealized, results!.TotalResultInEuros);

        var winner = results.ByAsset.Single(asset => asset.AssetSymbol == "ACME.US");
        var loser = results.ByAsset.Single(asset => asset.AssetSymbol == "ZEPH.DE");

        // Lo que el informe llama «Purchase Value» y «Sale Value», ya en euros. Tomar el
        // precio por la cantidad daría 580 y 720 dólares contados como euros.
        Assert.Equal(FakeXtbWorkbook.WinnerCost, winner.AcquisitionCostInEuros);
        Assert.Equal(FakeXtbWorkbook.WinnerProceeds, winner.ProceedsInEuros);
        Assert.Equal(FakeXtbWorkbook.LoserCost, loser.AcquisitionCostInEuros);
        Assert.Equal(FakeXtbWorkbook.LoserProceeds, loser.ProceedsInEuros);
    }

    [Fact]
    public async Task Importing_the_same_report_twice_changes_nothing()
    {
        var client = factory.CreateClientFor(Guid.NewGuid());
        var account = await AccountAsync(client, "XTB reimportada");

        await ConfirmAsync(client, await UploadAsync(client, account));

        var again = await UploadAsync(client, account);

        Assert.Equal(0, again.Run.RecordsImported);
        Assert.Equal(8, again.Run.DuplicatesDiscarded);

        await ConfirmAsync(client, again);

        var portfolio = await client.GetFromJsonAsync<PortfolioResponse>("/api/portfolio");

        Assert.Equal(FakeXtbWorkbook.ExpectedRealized, portfolio!.RealizedResultInEuros);
        Assert.Equal(FakeXtbWorkbook.ExpectedCash, portfolio.CashTotalInEuros);
    }

    private static async Task<Guid> AccountAsync(HttpClient client, string alias)
    {
        var account = await (await client.PostAsJsonAsync(
                "/api/accounts", new CreateAccountRequest("Xtb", alias, "EUR")))
            .Content.ReadFromJsonAsync<AccountResponse>();

        return account!.Id;
    }

    private static async Task<ImportPreviewResponse> UploadAsync(HttpClient client, Guid accountId)
    {
        using var book = FakeXtbWorkbook.Build();
        using var file = new StreamContent(book);
        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        using var content = new MultipartFormDataContent { { file, "file", "informe.xlsx" } };

        var response = await client.PostAsync($"/api/imports/file?accountId={accountId}", content);

        // El cuerpo lleva el detalle del problema. Perderlo dejaría el fallo en un número,
        // y averiguar la causa exigiría adivinar.
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<ImportPreviewResponse>())!;
    }

    private static async Task ConfirmAsync(HttpClient client, ImportPreviewResponse preview)
    {
        var response = await client.PostAsync($"/api/imports/{preview.Run.Id}/confirm", null);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
