using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Kapea.Client;
using Kapea.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Cultura fija en es-ES: los importes son en euros y las fechas del ejercicio fiscal
// español. Dejarlo al idioma del navegador cambiaría el separador decimal según quién
// mire, y una cifra fiscal no debería leerse distinta en dos equipos.
var culture = new System.Globalization.CultureInfo("es-ES");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalization();
builder.Services.AddFluentUIComponents();

// La API sirve además estos estáticos, así que su dirección es la del propio
// documento: un solo origen, sin CORS y sin nada que configurar por entorno.
// El estado de sesión va con un HttpClient desnudo, sin el manejador de caducidad:
// es ese manejador el que lo consulta, y compartirlo cerraría el círculo.
// Se registra a mano y no con AddHttpClient<SessionState> porque aquel lo dejaría
// transitorio: cada pantalla tendría su propia sesión y el encabezado no se enteraría
// de los cambios. Uno solo para toda la aplicación.
builder.Services.AddHttpClient("sesion", client =>
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

builder.Services.AddScoped(services =>
    new SessionState(services.GetRequiredService<IHttpClientFactory>().CreateClient("sesion")));

builder.Services.AddScoped<SessionExpiryHandler>();

builder.Services.AddHttpClient<KapeaApiClient>(client =>
        client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<SessionExpiryHandler>();

await builder.Build().RunAsync();
