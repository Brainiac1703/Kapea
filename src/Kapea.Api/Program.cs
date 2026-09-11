using Kapea.Api.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Kapea.Api.Endpoints;
using Kapea.Application.Abstractions;
using Kapea.Application.Credentials;
using Kapea.Application.Identity;
using Kapea.Application.Import;
using Kapea.Domain.Common;
using Kapea.Infrastructure;
using Kapea.Infrastructure.Import.Tabular;
using Kapea.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKapeaInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services.AddKapeaAuthentication(builder.Configuration, builder.Environment.IsDevelopment());

// Las claves que cifran la cookie viven en memoria si nadie dice lo contrario, así que
// cada reinicio echaba a todo el mundo de su sesión. En un directorio persistente
// sobreviven al despliegue; sin directorio configurado se sigue como hasta ahora.
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("Kapea");
}

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Solo si hay cadena de conexión: sin ella el exportador falla al arrancar, y una API
// no debería negarse a funcionar por no tener telemetría configurada.
if (!string.IsNullOrWhiteSpace(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // En desarrollo la base se crea sola al arrancar: levantar el entorno no debería
    // exigir acordarse de un comando aparte. En producción las migraciones se aplican
    // en el despliegue, donde el fallo se ve y se puede revertir.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<KapeaDbContext>().Database.MigrateAsync();

    if (string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"]))
    {
        app.Logger.LogWarning(
            "Sin proveedor configurado: la pantalla de acceso ofrece entrar como el usuario fijo de desarrollo.");
    }
}

// Los perfiles de serie se dan de alta en cada arranque si faltan. Es idempotente y
// barato, y evita que un despliegue nuevo quede sin saber leer ningún fichero.
await using (var profiles = app.Services.CreateAsyncScope())
{
    await Kapea.Infrastructure.Import.Tabular.BuiltInProfileSeeder.EnsureAsync(
        profiles.ServiceProvider.GetRequiredService<KapeaDbContext>(),
        profiles.ServiceProvider.GetRequiredService<TimeProvider>());
}

// Las reglas del dominio y los rechazos de las plataformas se traducen a respuestas
// con significado; el resto sale como error genérico y queda en las trazas.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    var (status, title) = exception switch
    {
        // Antes que DomainException, de la que deriva: el caso concreto explica mejor
        // qué ha pasado que el genérico.
        IdentityAlreadyLinkedException => (StatusCodes.Status409Conflict, "Esa cuenta ya está enlazada a otro usuario."),
        DomainException => (StatusCodes.Status409Conflict, "La operación no es válida en el estado actual."),
        CredentialRejectedException => (StatusCodes.Status400BadRequest, "La plataforma ha rechazado la credencial."),
        UnsupportedPlatformException => (StatusCodes.Status400BadRequest, "Plataforma no soportada."),
        UnsupportedImportFileException => (StatusCodes.Status400BadRequest, "Tipo de fichero no admitido."),
        UnknownFileFormatException => (StatusCodes.Status422UnprocessableEntity, "Ningún perfil reconoce el formato del fichero."),
        ImportTargetException => (StatusCodes.Status404NotFound, "No se encuentra el destino de la importación."),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autenticado."),
        _ => (StatusCodes.Status500InternalServerError, "Error inesperado."),
    };

    await Results
        .Problem(title: title, detail: exception?.Message, statusCode: status)
        .ExecuteAsync(context);
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// La API sirve además los estáticos del cliente WebAssembly. Un solo origen evita el
// CORS y es como se despliega: un contenedor con la API y el cliente dentro.
//
// MapStaticAssets y no UseStaticFiles: el manifiesto de recursos estáticos publica
// cada fichero del cliente con su nombre fijo además del que lleva huella. Sirviendo
// directamente del disco solo existe el segundo, y el runtime de Blazor daría 404
// con la página cargando en blanco y sin ningún error en el servidor.
app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();
app.MapKapeaEndpoints();

// Cualquier ruta que no sea de la API la resuelve el enrutador de Blazor en el cliente.
app.MapFallbackToFile("index.html");

await app.RunAsync();

/// <summary>Punto de entrada visible para las pruebas de integración de la API.</summary>
public partial class Program;
