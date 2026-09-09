using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Ejecuta un bloque dentro de una transacción respetando la estrategia de reintento.
/// </summary>
/// <remarks>
/// Con reintentos activados, EF se niega a que alguien abra una transacción por su
/// cuenta: si el bloque se reintentara con una transacción ya abierta fuera, la
/// segunda vuelta escribiría sobre cambios a medio deshacer. La transacción tiene que
/// abrirse dentro de lo que se reintenta, y por eso esto vive en un solo sitio en vez
/// de repetirse en cada almacén.
/// </remarks>
internal static class TransactionalExecution
{
    internal static async Task ExecuteAsync(
        this DatabaseFacade database,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(work);

        await database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await database
                .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await work(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
