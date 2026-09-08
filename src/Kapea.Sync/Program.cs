using Kapea.Application.Synchronization;
using Kapea.Infrastructure;
using Kapea.Sync;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKapeaInfrastructure(builder.Configuration);
builder.Services.Configure<SynchronizationOptions>(
    builder.Configuration.GetSection(SynchronizationOptions.SectionName));

// Observabilidad: la sincronización habla con terceros y falla de formas que solo se
// entienden mirando las trazas de una ejecución concreta.
builder.Services.AddApplicationInsightsTelemetryWorkerService();

builder.Services.AddHostedService<SynchronizationWorker>();

await builder.Build().RunAsync();
