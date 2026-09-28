using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Kapea.Client.Resources;
using Kapea.Client.Services;
using Kapea.Shared.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Kapea.Client.Tests;

/// <summary>
/// Falta serie y falta cotización no se remedian igual, así que el aviso tiene que
/// decir cuál falta. Un aviso único acertaba y engañaba a la vez.
/// </summary>
public class WatchlistNoticeTests : BunitContext
{
    private readonly AddHandler _handler = new();

    public WatchlistNoticeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddFluentUIComponents();
        Services.AddScoped(_ => new KapeaApiClient(
            new HttpClient(_handler) { BaseAddress = new Uri("https://prueba.local/") }));
    }

    [Fact]
    public void An_asset_with_history_but_no_quote_is_told_to_add_it_from_a_search()
    {
        var page = Added(hasSeries: true, hasQuote: false);

        Assert.Contains(Text("Watchlist_WithoutQuote"), page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_asset_with_a_quote_but_no_history_is_told_it_will_have_no_chart()
    {
        var page = Added(hasSeries: false, hasQuote: true);

        Assert.Contains(Text("Watchlist_WithoutSeries"), page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_asset_nobody_covers_keeps_the_warning_it_already_had()
    {
        var page = Added(hasSeries: false, hasQuote: false);

        Assert.Contains(Text("Watchlist_WithoutPrices"), page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_asset_covered_by_both_says_nothing()
    {
        var page = Added(hasSeries: true, hasQuote: true);

        foreach (var aviso in new[] { "Watchlist_WithoutQuote", "Watchlist_WithoutSeries", "Watchlist_WithoutPrices" })
        {
            Assert.DoesNotContain(Text(aviso), page.Markup, StringComparison.Ordinal);
        }
    }

    private IRenderedComponent<Kapea.Client.Pages.Watchlist> Added(bool hasSeries, bool hasQuote)
    {
        _handler.Answer(hasSeries, hasQuote);

        var page = Render<Kapea.Client.Pages.Watchlist>();

        // Seguir por símbolo es la salida cuando la búsqueda no encuentra nada, así que
        // hay que pasar por ella para llegar al botón.
        page.Find("fluent-search").Change("QNT");

        page.FindAll("fluent-button")
            .Single(button => button.TextContent.Contains(Text("Watchlist_AddBySymbol"), StringComparison.Ordinal))
            .Click();

        return page;
    }

    private string Text(string key) =>
        Services.GetRequiredService<IStringLocalizer<UiStrings>>()[key].Value;

    /// <summary>Devuelve la lista vacía y, al dar de alta, la cobertura que se le pida.</summary>
    private sealed class AddHandler : HttpMessageHandler
    {
        private bool _hasSeries;
        private bool _hasQuote;

        internal void Answer(bool hasSeries, bool hasQuote)
        {
            _hasSeries = hasSeries;
            _hasQuote = hasQuote;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var asset = new WatchedAssetResponse(
                Guid.NewGuid(), "QNT", "Quant", "Crypto", false, null, null, null, null);

            var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var ruta = request.RequestUri!.AbsolutePath;

            var body = request.Method == HttpMethod.Post
                ? JsonSerializer.Serialize(new WatchAssetResponse(asset, false, _hasSeries, _hasQuote), opciones)
                : ruta.EndsWith("/search", StringComparison.Ordinal)
                    ? JsonSerializer.Serialize(new AssetSearchResponse([], IsComplete: true), opciones)
                    : "[]";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
