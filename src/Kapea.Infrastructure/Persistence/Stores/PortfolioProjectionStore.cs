using Kapea.Application.Abstractions;
using Kapea.Domain.Calculation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>
/// Reemplaza la proyección de un activo dentro de una transacción.
/// </summary>
/// <remarks>
/// Se borra y se reinserta en vez de conciliar diferencias: a la escala de un usuario
/// particular el coste es despreciable, y elimina de raíz la clase de errores en la
/// que un lote consumido sobrevive a un recálculo y descuadra el ejercicio.
/// </remarks>
public sealed class PortfolioProjectionStore(KapeaDbContext context, ILogger<PortfolioProjectionStore> logger)
    : IPortfolioProjectionStore
{
    public async Task ReplaceAsync(
        Guid assetId,
        AssetCalculationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // El filtro global limita el borrado al usuario actual, así que un recálculo
        // nunca puede llevarse por delante la proyección de otro.
        await context.Lots.Where(lot => lot.AssetId == assetId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await context.RealizedResults.Where(realized => realized.AssetId == assetId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await context.CapitalIncomes.Where(income => income.AssetId == assetId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        context.Lots.AddRange(result.Lots);
        context.RealizedResults.AddRange(result.RealizedResults);
        context.CapitalIncomes.AddRange(result.CapitalIncomes);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Proyección del activo {Activo} reemplazada: {Lotes} lotes, {Resultados} resultados, {Rendimientos} rendimientos.",
            assetId, result.Lots.Count, result.RealizedResults.Count, result.CapitalIncomes.Count);
    }
}
