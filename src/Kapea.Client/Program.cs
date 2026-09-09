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
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<KapeaApiClient>();

await builder.Build().RunAsync();
