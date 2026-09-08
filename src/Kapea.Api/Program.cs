using Kapea.Api.Authentication;
using Kapea.Api.Endpoints;
using Kapea.Application.Abstractions;
using Kapea.Application.Credentials;
using Kapea.Application.Import;
using Kapea.Domain.Common;
using Kapea.Infrastructure;
using Kapea.Infrastructure.Import.Xtb;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKapeaInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Entra External ID desde el principio: rehacer la capa de identidad más adelante
// obligaría a tocar el aislamiento entre usuarios, que es justo lo que no conviene mover.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
// Solo si hay cadena de conexión: sin ella el exportador falla al arrancar, y una API
// no debería negarse a funcionar por no tener telemetría configurada.
if (!string.IsNullOrWhiteSpace(builder.Configuration["ApplicationInsights:ConnectionString"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}
builder.Services.AddOpenApi();

var app = builder.Build();

// Las reglas del dominio y los rechazos de las plataformas se traducen a respuestas
// con significado; el resto sale como error genérico y queda en las trazas.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

    var (status, title) = exception switch
    {
        DomainException => (StatusCodes.Status409Conflict, "La operación no es válida en el estado actual."),
        CredentialRejectedException => (StatusCodes.Status400BadRequest, "La plataforma ha rechazado la credencial."),
        UnsupportedPlatformException => (StatusCodes.Status400BadRequest, "Plataforma no soportada."),
        UnsupportedImportFileException => (StatusCodes.Status400BadRequest, "Tipo de fichero no admitido."),
        UnknownXtbFormatException => (StatusCodes.Status422UnprocessableEntity, "El formato del fichero no se reconoce."),
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

app.UseAuthentication();
app.UseAuthorization();

app.MapKapeaEndpoints();

await app.RunAsync();

/// <summary>Punto de entrada visible para las pruebas de integración de la API.</summary>
public partial class Program;
