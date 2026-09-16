using Kapea.Application.Portfolio;
using Kapea.Domain.Assets;
using Kapea.Domain.Common;
using Kapea.Domain.Transactions;
using Kapea.Shared.Contracts;

namespace Kapea.Api.Endpoints;

/// <summary>
/// Movimientos apuntados a mano y correcciones de importados.
/// </summary>
/// <remarks>
/// Cada escritura termina recalculando, igual que confirmar una importación: la cartera
/// que se consulta justo después ya tiene que reflejar el cambio. Los rechazos del dominio
/// salen como 409 por el manejador general, con su mensaje.
/// </remarks>
internal static class ManualMovementEndpoints
{
    internal static void MapManualMovements(this RouteGroupBuilder api)
    {
        var movements = api.MapGroup("/transactions");

        movements.MapPost("/", async (
            ManualMovementRequest request,
            ManualMovementService service,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            var movement = await service.RegisterAsync(Input(request), token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.Ok(new { movement.Id });
        });

        movements.MapPut("/{transactionId:guid}", async (
            Guid transactionId,
            ManualMovementRequest request,
            ManualMovementService service,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            var movement = await service.ReviseAsync(transactionId, Input(request), token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.Ok(new { movement.Id });
        });

        movements.MapDelete("/{transactionId:guid}", async (
            Guid transactionId,
            ManualMovementService service,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            await service.DeleteAsync(transactionId, token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.NoContent();
        });

        movements.MapPost("/{transactionId:guid}/correct", async (
            Guid transactionId,
            ManualMovementRequest request,
            ManualMovementService service,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            var adjustment = await service.CorrectAsync(transactionId, Input(request), token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.Ok(new { adjustment.Id });
        });

        movements.MapPost("/{transactionId:guid}/void", async (
            Guid transactionId,
            VoidMovementRequest request,
            ManualMovementService service,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            await service.VoidAsync(transactionId, request.Reason, token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.NoContent();
        });

        movements.MapPost("/{transactionId:guid}/restore", async (
            Guid transactionId,
            ManualMovementService service,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            await service.RestoreAsync(transactionId, token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.NoContent();
        });

        // Sin recálculo: marcar que son distintos no cambia ninguna cifra, sólo deja de
        // señalar la pareja.
        movements.MapPost("/{manualId:guid}/distinct/{importedId:guid}", async (
            Guid manualId,
            Guid importedId,
            ManualMovementService service,
            CancellationToken token) =>
        {
            await service.MarkDistinctAsync(manualId, importedId, token);

            return Results.NoContent();
        });

        movements.MapGet("/impact", (DateOnly date, DateOnly? previous, TimeProvider time) =>
        {
            var impact = CorrectionImpact.For(date, previous, DateOnly.FromDateTime(time.GetLocalNow().DateTime));

            return Results.Ok(new CorrectionImpactResponse(impact.TaxYear, impact.IsPastYear));
        });

        api.MapGet("/review/manual-duplicates", (IPortfolioQueries queries, CancellationToken token) =>
            queries.ListManualDuplicatesAsync(token));
    }

    /// <summary>
    /// Traduce la petición al formulario del servicio.
    /// </summary>
    /// <remarks>
    /// Un tipo o una clase desconocidos se rechazan aquí, con un mensaje que dice cuál, en
    /// lugar de convertirse en un movimiento sin clasificar que nadie ha pedido.
    /// </remarks>
    private static ManualMovementInput Input(ManualMovementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<TransactionType>(request.Type, ignoreCase: true, out var type) || !Enum.IsDefined(type))
        {
            throw new DomainException($"El tipo de movimiento «{request.Type}» no existe.");
        }

        AssetClass? assetClass = null;

        if (request.AssetClass is { Length: > 0 } requested)
        {
            if (!Enum.TryParse<AssetClass>(requested, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            {
                throw new DomainException($"La clase de activo «{requested}» no existe.");
            }

            assetClass = parsed;
        }

        return new ManualMovementInput(
            request.AccountId,
            type,
            request.AssetSymbol,
            assetClass,
            request.Quantity,
            request.UnitPrice,
            request.GrossAmount,
            request.Currency,
            request.Fee,
            request.OccurredAt,
            request.TimeZoneId,
            request.Text);
    }
}
