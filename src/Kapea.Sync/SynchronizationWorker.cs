using Kapea.Application.Synchronization;

namespace Kapea.Sync;

/// <summary>
/// Dispara la sincronización periódica.
/// </summary>
/// <remarks>
/// Vive en su propio proceso y no dentro de la API porque su ciclo de vida es distinto
/// —ejecución programada, sin tráfico entrante— y porque conviene que un fallo de la
/// parte que depende de terceros no arrastre a la API.
/// </remarks>
public sealed class SynchronizationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<SynchronizationOptions> options,
    ILogger<SynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval;

        logger.LogInformation("Sincronización programada cada {Intervalo}.", interval);

        using var timer = new PeriodicTimer(interval, timeProvider);

        do
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        // Un fallo no puede tumbar el proceso: la siguiente ejecución tiene que llegar.
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();

            await scope.ServiceProvider
                .GetRequiredService<SynchronizationService>()
                .RunAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "La sincronización programada ha fallado por completo.");
        }
    }
}

/// <summary>Cada cuánto se dispara la sincronización.</summary>
public sealed class SynchronizationOptions
{
    public const string SectionName = "Synchronization";

    /// <summary>
    /// Seis horas por defecto: suficiente para que la cartera esté al día sin gastar
    /// los límites de frecuencia de las plataformas en consultas que no traen nada.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
}
