using Kapea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Import.Tabular;

/// <summary>
/// Da de alta los perfiles que vienen de serie si todavía no están.
/// </summary>
/// <remarks>
/// Va aquí y no en la migración porque un perfil lleva sus reglas en columnas JSON y
/// una fila sembrada a mano en SQL sería ilegible e imposible de mantener al día.
///
/// Se reconoce por el nombre y no por el identificador: así, un perfil de serie que el
/// usuario haya corregido no se duplica ni se pisa en el siguiente arranque. Corregirlo
/// es la vía prevista cuando el bróker cambia sus cabeceras.
/// </remarks>
public static class BuiltInProfileSeeder
{
    public static async Task EnsureAsync(
        KapeaDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var existing = await context.ImportProfiles
            .Select(profile => profile.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var known = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = BuiltInProfiles.All(timeProvider.GetUtcNow())
            .Where(profile => !known.Contains(profile.Name))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.ImportProfiles.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
