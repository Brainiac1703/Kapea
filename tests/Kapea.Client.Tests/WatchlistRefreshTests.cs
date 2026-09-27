using System.Net;
using System.Text;
using Bunit;
using Kapea.Client.Resources;
using Kapea.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Kapea.Client.Tests;

/// <summary>
/// El precio no se refresca solo, así que el botón es la única forma de volver a
/// pedirlo sin recargar el navegador entero. Si desaparece, la pantalla se queda
/// enseñando el precio del momento en que se abrió sin que nada lo diga.
/// </summary>
public class WatchlistRefreshTests : BunitContext
{
    private readonly CountingHandler _handler = new();

    public WatchlistRefreshTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddFluentUIComponents();
        Services.AddScoped(_ => new KapeaApiClient(
            new HttpClient(_handler) { BaseAddress = new Uri("https://prueba.local/") }));
    }

    [Fact]
    public void The_watchlist_asks_for_the_list_again_when_refreshed()
    {
        var page = Render<Kapea.Client.Pages.Watchlist>();

        Assert.Equal(1, _handler.Calls);

        page.FindAll("fluent-button")
            .Single(button => button.TextContent.Contains(Text("Watchlist_Refresh"), StringComparison.Ordinal))
            .Click();

        Assert.Equal(2, _handler.Calls);
    }

    [Fact]
    public void The_refresh_button_has_a_localized_label()
    {
        // El escáner de literales sólo mira el marcado: una clave que no existe en los
        // recursos pasaría igual y saldría en pantalla con su propio nombre dentro.
        var label = Text("Watchlist_Refresh");

        Assert.NotEqual("Watchlist_Refresh", label);
        Assert.NotEmpty(label);
    }

    private string Text(string key) =>
        Services.GetRequiredService<IStringLocalizer<UiStrings>>()[key].Value;

    /// <summary>Cuenta las peticiones de la lista y devuelve siempre una vacía.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        internal int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json"),
            });
        }
    }
}
