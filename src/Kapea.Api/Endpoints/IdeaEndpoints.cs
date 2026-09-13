using Kapea.Application.Abstractions;
using Kapea.Application.Ideas;
using Kapea.Domain.Ideas;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Api.Endpoints;

/// <summary>
/// Ideas que llegan de fuera y lo que dieron.
/// </summary>
/// <remarks>
/// Del texto de una publicación ajena no se guarda nada: entra, se extraen las ideas y
/// se descarta. Lo que queda es la idea, su enlace y, con el tiempo, su desenlace.
/// </remarks>
internal static class IdeaEndpoints
{
    internal static void MapIdeas(this RouteGroupBuilder api)
    {
        var ideas = api.MapGroup("/ideas");

        ideas.MapGet("/sources", async (
            KapeaDbContext context,
            ISourceWatcher watcher,
            CancellationToken token) =>
            (await context.IdeaSources.OrderBy(source => source.Name).ToListAsync(token))
                .Select(source => new IdeaSourceResponse(
                    source.Id,
                    source.Name,
                    source.Channel,
                    source.LastSeenAt,
                    source.LastSeenUrl,
                    watcher.IsAvailable && source.Channel is { Length: > 0 }))
                .ToList());

        ideas.MapPost("/sources", async (
            CreateIdeaSourceRequest request,
            KapeaDbContext context,
            ICurrentUser user,
            CancellationToken token) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            var source = IdeaSource.Create(user.Id, request.Name, request.Channel);

            context.IdeaSources.Add(source);
            await context.SaveChangesAsync(token);

            return Results.Created($"/api/ideas/sources/{source.Id}", new IdeaSourceResponse(
                source.Id, source.Name, source.Channel, null, null, false));
        });

        // Extraer no guarda nada: devuelve lo sacado para que una persona lo revise.
        ideas.MapPost("/extract", async (
            ExtractIdeasRequest request,
            IIdeaExtractor extractor,
            TimeProvider time,
            CancellationToken token) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!extractor.IsAvailable)
            {
                return Results.Ok(new IdeaExtractionResponse(
                    false, [], [], "No hay servicio de extracción configurado."));
            }

            var extraction = await extractor.ExtractAsync(request.Text, token);

            if (extraction is null)
            {
                return Results.Ok(new IdeaExtractionResponse(
                    true, [], [], "No se ha podido sacar ninguna idea de ese texto."));
            }

            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

            return Results.Ok(new IdeaExtractionResponse(
                true,
                [
                    .. extraction.Ideas.Select(idea => new CreateIdeaRequest(
                        Guid.Empty,
                        idea.Symbol,
                        idea.Direction,
                        today,
                        idea.Entry,
                        idea.Target,
                        idea.StopLoss,
                        null,
                        idea.Note)),
                ],
                extraction.NotUnderstood,
                extraction.Ideas.Count == 0 ? "El texto no trae ninguna idea concreta." : null));
        });

        ideas.MapPost("/", async (
            CreateIdeaRequest request,
            KapeaDbContext context,
            ICurrentUser user,
            CancellationToken token) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            // El símbolo se resuelve contra el catálogo para poder seguir la idea, pero
            // se guarda además tal como lo nombró la fuente.
            var assetId = await context.Assets
                .Where(asset => asset.CanonicalSymbol == request.Symbol.ToUpperInvariant())
                .Select(asset => (Guid?)asset.Id)
                .FirstOrDefaultAsync(token);

            var idea = ExternalIdea.Create(
                user.Id,
                request.SourceId,
                request.Symbol,
                string.Equals(request.Direction, "Sell", StringComparison.OrdinalIgnoreCase)
                    ? IdeaDirection.Sell
                    : IdeaDirection.Buy,
                request.PublishedOn,
                assetId,
                request.Entry is { } entry ? Money.Euros(entry) : null,
                request.Target is { } target ? Money.Euros(target) : null,
                request.StopLoss is { } stop ? Money.Euros(stop) : null,
                request.Url,
                request.Note);

            context.ExternalIdeas.Add(idea);
            await context.SaveChangesAsync(token);

            return Results.Created($"/api/ideas/{idea.Id}", await ToResponseAsync(context, idea, token));
        });

        ideas.MapGet("/", async (KapeaDbContext context, CancellationToken token) =>
        {
            var all = await context.ExternalIdeas
                .OrderByDescending(idea => idea.PublishedOn)
                .ToListAsync(token);

            var names = await context.IdeaSources
                .ToDictionaryAsync(source => source.Id, source => source.Name, token);

            return all.Select(idea => ToResponse(idea, names)).ToList();
        });

        // Poner al día los desenlaces es una acción aparte: recorre la serie de precios y
        // no conviene hacerlo cada vez que se abre la pantalla.
        ideas.MapPost("/track", async (
            KapeaDbContext context,
            IPriceHistoryStore prices,
            TimeProvider time,
            CancellationToken token) =>
        {
            var open = await context.ExternalIdeas
                .Where(idea => idea.Outcome == IdeaOutcome.Open)
                .ToListAsync(token);

            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var resolved = 0;

            foreach (var idea in open.Where(idea => idea.CanBeTracked))
            {
                var series = await prices
                    .GetAsync(idea.AssetId!.Value, idea.PublishedOn, today, token)
                    .ConfigureAwait(false);

                var resolution = IdeaTracking.Resolve(idea, series, today);

                if (resolution.Outcome == IdeaOutcome.Open || resolution.On is not { } on)
                {
                    continue;
                }

                idea.Resolve(resolution.Outcome, on);
                resolved++;
            }

            await context.SaveChangesAsync(token);

            return new { Revisadas = open.Count, Resueltas = resolved };
        });

        ideas.MapGet("/balance", async (
            KapeaDbContext context,
            CancellationToken token) =>
        {
            var all = await context.ExternalIdeas.ToListAsync(token);

            var sources = await context.IdeaSources.ToListAsync(token);

            // El coste que se aplica es el más alto de las plataformas donde opera, por lo
            // mismo que en el simulador: seguir una idea cuesta lo mismo que seguir una
            // señal, y una fuente que acierta dos de cada tres puede salir perdiendo.
            var fee = await context.Platforms.MaxAsync(platform => (decimal?)platform.FeeRate, token) ?? 0m;

            return sources
                .Select(source => SourceBalanceCalculator.Of(
                    source.Id,
                    source.Name,
                    [.. all.Where(idea => idea.SourceId == source.Id)],
                    fee))
                .Select(balance => new SourceBalanceResponse(
                    balance.SourceId,
                    balance.SourceName,
                    balance.Reached,
                    balance.Stopped,
                    balance.Expired,
                    balance.Open,
                    balance.ReturnNetOfFees))
                .ToList();
        });
    }

    private static async Task<IdeaResponse> ToResponseAsync(
        KapeaDbContext context,
        ExternalIdea idea,
        CancellationToken token)
    {
        var name = await context.IdeaSources
            .Where(source => source.Id == idea.SourceId)
            .Select(source => source.Name)
            .FirstOrDefaultAsync(token) ?? string.Empty;

        return ToResponse(idea, new Dictionary<Guid, string> { [idea.SourceId] = name });
    }

    private static IdeaResponse ToResponse(ExternalIdea idea, IReadOnlyDictionary<Guid, string> sources) =>
        new(
            idea.Id,
            idea.SourceId,
            sources.GetValueOrDefault(idea.SourceId, string.Empty),
            idea.Symbol,
            idea.AssetId,
            idea.Direction.ToString(),
            idea.PublishedOn,
            idea.Entry?.Amount,
            idea.Target?.Amount,
            idea.StopLoss?.Amount,
            idea.Url,
            idea.Note,
            idea.Outcome.ToString(),
            idea.ResolvedOn);
}
