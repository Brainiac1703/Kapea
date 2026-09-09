using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ImportProfiles;
using Kapea.Infrastructure.Persistence;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Api.Endpoints;

/// <summary>Perfiles de importación: qué reglas leen cada formato de fichero.</summary>
public static class ImportProfileEndpoints
{
    public static void MapImportProfileEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var profiles = api.MapGroup("/profiles");

        // Los campos del movimiento a los que puede apuntar una columna. Los da el
        // servidor para que la pantalla de mapeo no tenga que traerlos escritos y se
        // quede corta en cuanto aparezca uno nuevo.
        profiles.MapGet("/fields", () => Results.Ok(ImportProfileRules.Fields()));

        profiles.MapGet("/", async (string? platform, KapeaDbContext context, CancellationToken token) =>
        {
            var query = context.ImportProfiles.AsQueryable();

            if (PlatformCode.TryParse(platform, out var code))
            {
                var wanted = code.Value;
                query = query.Where(profile => profile.Platform == wanted);
            }

            var found = await query.ToListAsync(token);

            return Results.Ok(found
                .OrderBy(profile => profile.Platform.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .Select(ImportProfileRules.ToResponse)
                .ToArray());
        });

        profiles.MapGet("/{profileId:guid}", async (
            Guid profileId, KapeaDbContext context, CancellationToken token) =>
            await context.ImportProfiles.SingleOrDefaultAsync(profile => profile.Id == profileId, token) is { } profile
                ? Results.Ok(ImportProfileRules.ToResponse(profile))
                : Results.NotFound());

        profiles.MapPost("/", async (
            CreateImportProfileRequest request,
            KapeaDbContext context,
            TimeProvider clock,
            CancellationToken token) =>
        {
            if (!PlatformCode.TryParse(request.Platform, out var code))
            {
                return Results.Problem(
                    $"Plataforma '{request.Platform}' no reconocida.", statusCode: StatusCodes.Status400BadRequest);
            }

            var platform = code.Value;

            if (!await context.Platforms.AnyAsync(entity => entity.Code == platform, token))
            {
                return Results.Problem(
                    $"La plataforma '{platform}' no está en el catálogo.", statusCode: StatusCodes.Status400BadRequest);
            }

            var profile = ImportProfile.Create(
                platform,
                request.Name,
                number => ImportProfileRules.ToVersion(request.Rules, number, clock.GetUtcNow()));

            context.ImportProfiles.Add(profile);
            await context.SaveChangesAsync(token);

            return Results.Created($"/api/profiles/{profile.Id}", ImportProfileRules.ToResponse(profile));
        });

        // Corregir un perfil no reescribe sus reglas: guarda una versión nueva. Los
        // movimientos ya importados siguen apuntando a la anterior, que es lo que
        // permite explicar de dónde salió una cifra de hace meses.
        profiles.MapPost("/{profileId:guid}/versions", async (
            Guid profileId,
            ReviseImportProfileRequest request,
            KapeaDbContext context,
            TimeProvider clock,
            CancellationToken token) =>
        {
            var profile = await context.ImportProfiles
                .SingleOrDefaultAsync(entity => entity.Id == profileId, token);

            if (profile is null)
            {
                return Results.NotFound();
            }

            profile.Revise(number => ImportProfileRules.ToVersion(request.Rules, number, clock.GetUtcNow()));

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                profile.Rename(request.Name);
            }

            await context.SaveChangesAsync(token);

            return Results.Ok(ImportProfileRules.ToResponse(profile));
        });

        profiles.MapDelete("/{profileId:guid}", async (
            Guid profileId, KapeaDbContext context, CancellationToken token) =>
        {
            var profile = await context.ImportProfiles
                .SingleOrDefaultAsync(entity => entity.Id == profileId, token);

            if (profile is null)
            {
                return Results.NotFound();
            }

            if (profile.BuiltIn)
            {
                throw new DomainException(
                    $"El perfil '{profile.Name}' viene de serie. Corrígelo en lugar de borrarlo: se vuelve a crear en el siguiente arranque.");
            }

            context.ImportProfiles.Remove(profile);
            await context.SaveChangesAsync(token);

            return Results.NoContent();
        });
    }
}
