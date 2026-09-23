using Kapea.Application.Abstractions;
using Kapea.Domain.Calculation;
using Kapea.Domain.Lots;
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
    public async Task<IReadOnlyCollection<Guid>> ListProjectedAssetsAsync(CancellationToken cancellationToken = default)
    {
        var lots = await context.Lots.Select(lot => lot.AssetId).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
        var realized = await context.RealizedResults.Select(result => result.AssetId).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);
        var income = await context.CapitalIncomes
            .Where(entry => entry.AssetId != null)
            .Select(entry => entry.AssetId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var inconsistencies = await context.CalculationInconsistencies
            .Select(inconsistency => inconsistency.AssetId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return lots.Concat(realized).Concat(income).Concat(inconsistencies).ToHashSet();
    }

    public async Task ReplaceAsync(
        Guid assetId,
        AssetCalculationResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        await context.Database.ExecuteAsync(async token =>
        {
            // El filtro global limita el borrado al usuario actual, así que un recálculo
            // nunca puede llevarse por delante la proyección de otro.
            await context.Lots.Where(lot => lot.AssetId == assetId)
                .ExecuteDeleteAsync(token).ConfigureAwait(false);

            await context.RealizedResults.Where(realized => realized.AssetId == assetId)
                .ExecuteDeleteAsync(token).ConfigureAwait(false);

            await context.CapitalIncomes.Where(income => income.AssetId == assetId)
                .ExecuteDeleteAsync(token).ConfigureAwait(false);

            await context.CalculationInconsistencies.Where(inconsistency => inconsistency.AssetId == assetId)
                .ExecuteDeleteAsync(token).ConfigureAwait(false);

            // ExecuteDelete borra en la base pero no descarta lo que el contexto ya tenía
            // en memoria. Sin soltarlo, añadir la proyección nueva choca con la vieja por
            // clave repetida, que es lo que pasa al recalcular justo después de importar.
            Forget<Lot>(assetId);
            Forget<RealizedResult>(assetId);
            Forget<CapitalIncome>(assetId);
            Forget<CalculationInconsistency>(assetId);

            context.Lots.AddRange(result.Lots);
            context.RealizedResults.AddRange(result.RealizedResults);
            context.CapitalIncomes.AddRange(result.CapitalIncomes);
            context.CalculationInconsistencies.AddRange(result.Inconsistencies);

            await context.SaveChangesAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Proyección del activo {Activo} reemplazada: {Lotes} lotes, {Resultados} resultados, {Rendimientos} rendimientos, {Incoherencias} incoherencias.",
            assetId, result.Lots.Count, result.RealizedResults.Count, result.CapitalIncomes.Count, result.Inconsistencies.Count);
    }

    /// <summary>Suelta del contexto la proyección de un activo que ya se ha borrado.</summary>
    private void Forget<T>(Guid assetId)
        where T : class
    {
        var stale = context.ChangeTracker
            .Entries<T>()
            .Where(entry => entry.Property("AssetId").CurrentValue is Guid tracked && tracked == assetId)
            .ToList();

        foreach (var entry in stale)
        {
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
    }
}
