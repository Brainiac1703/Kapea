using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ImportProfiles;
using Kapea.Infrastructure.Import.Mapping;
using Kapea.Infrastructure.Persistence;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        // Mira el fichero sin importar nada: es lo primero que hace falta cuando ningún
        // perfil lo reconoce y hay que darlo de alta.
        profiles.MapPost("/inspect", async (
            Guid accountId,
            IFormFile file,
            KapeaDbContext context,
            IFileInspector inspector,
            IMappingProposer proposer,
            CancellationToken token) =>
        {
            var account = await context.Accounts.SingleOrDefaultAsync(entity => entity.Id == accountId, token);

            if (account is null)
            {
                return Results.NotFound();
            }

            await using var content = file.OpenReadStream();
            var inspection = await inspector.InspectAsync(account.Platform, content, file.FileName, token);

            return Results.Ok(new FileInspectionResponse(
                inspection.Headers,
                inspection.SampleRows,
                inspection.MatchedProfileId,
                inspection.MatchedProfileName,
                proposer.IsAvailable));
        }).DisableAntiforgery();

        // La propuesta se pide aparte y a mano. Pedirla sola al mirar el fichero
        // enviaría las filas antes de que nadie haya visto el aviso de qué sale.
        profiles.MapPost("/propose", async (
            string platform,
            MappingSampleRequest request,
            IMappingProposer proposer,
            IOptions<AzureOpenAiOptions> options,
            CancellationToken token) =>
        {
            if (!PlatformCode.TryParse(platform, out var code))
            {
                return Results.Problem(
                    $"Plataforma '{platform}' no reconocida.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!proposer.IsAvailable)
            {
                return Results.Problem(
                    "No hay servicio de propuestas configurado. El mapeo se hace a mano.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var sample = new MappingSample(
                request.Headers ?? [],
                [.. (request.Rows ?? []).Select(row => (IReadOnlyList<string>)row)]);

            var proposal = await proposer.ProposeAsync(code.Value, sample, token);

            if (proposal is null)
            {
                return Results.Problem(
                    "No se ha podido obtener una propuesta. Puedes mapearlo a mano.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var review = MappingReview.Review(proposal, sample, options.Value.ConfidenceThreshold);

            return Results.Ok(new MappingProposalResponse(
                proposal.Fields.ToDictionary(field => field.Field.ToString(), field => field.Column),
                proposal.Fields.ToDictionary(field => field.Field.ToString(), field => field.Confidence),
                proposal.Concepts.ToDictionary(concept => concept.Concept, concept => concept.Type.ToString()),
                proposal.DecimalConvention.ToString(),
                proposal.DateFormats,
                proposal.RowShape.ToString(),
                proposal.AmountSource.ToString(),
                proposal.Delimiter.ToString(),
                proposal.FixedCurrency,
                review.IsConclusive,
                [.. review.Doubts.Select(doubt => doubt.Explanation)]));
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
