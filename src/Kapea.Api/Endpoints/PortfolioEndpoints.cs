using Kapea.Application.Credentials;
using Kapea.Application.Import;
using Kapea.Application.Portfolio;
using Kapea.Application.Abstractions;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.Transfers;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Api.Endpoints;

/// <summary>
/// Endpoints que consume el cliente Blazor. La API es la única pieza que ve las
/// credenciales y la base de datos; el cliente solo habla con ella.
/// </summary>
public static class PortfolioEndpoints
{
    /// <summary>Tamaño máximo de una exportación. Un extracto de años no llega a estos órdenes.</summary>
    private const long MaximumUploadBytes = 32 * 1024 * 1024;

    public static void MapKapeaEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapAccounts();
        api.MapImportProfileEndpoints();
        api.MapCredentials();
        api.MapImports();
        api.MapPortfolio();
        api.MapTransfers();
        api.MapStrategies();
        api.MapJournal();
        api.MapIdeas();
    }

    private static void MapAccounts(this RouteGroupBuilder api)
    {
        var platforms = api.MapGroup("/platforms");

        platforms.MapGet("/", (IPortfolioQueries queries, CancellationToken token) =>
            queries.ListPlatformsAsync(token));

        // Alta de una plataforma que no viene de serie. Solo de fichero: una de API
        // necesita además su adaptador, y darla de alta sin él dejaría una entrada que
        // no se puede usar y que solo se descubre al intentar sincronizar.
        platforms.MapPost("/", async (
            CreatePlatformRequest request,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            if (!Enum.TryParse<PlatformImportKind>(request.ImportKind, ignoreCase: true, out var importKind))
            {
                return Results.Problem(
                    $"Forma de importación '{request.ImportKind}' desconocida. Admitidas: {string.Join(", ", Enum.GetNames<PlatformImportKind>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (importKind != PlatformImportKind.File)
            {
                return Results.Problem(
                    "Una plataforma de API necesita su adaptador, que es código. Desde aquí solo se dan de alta las de fichero.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var code = new PlatformCode(request.Code);

            if (await context.Platforms.AnyAsync(entity => entity.Code == code, token))
            {
                return Results.Problem(
                    $"Ya hay una plataforma con el código '{code}'.", statusCode: StatusCodes.Status409Conflict);
            }

            var platform = Platform.Create(code, request.Name, importKind);

            context.Platforms.Add(platform);
            await context.SaveChangesAsync(token);

            return Results.Created(
                $"/api/platforms/{platform.Code}",
                new PlatformResponse(platform.Code.Value, platform.Name, platform.ImportKind.ToString(), platform.BuiltIn));
        });

        platforms.MapDelete("/{code}", async (
            string code,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            if (!PlatformCode.TryParse(code, out var parsed))
            {
                return Results.NotFound();
            }

            var platformCode = parsed.Value;
            var platform = await context.Platforms
                .SingleOrDefaultAsync(entity => entity.Code == platformCode, token);

            if (platform is null)
            {
                return Results.NotFound();
            }

            // Cuenta sobre todos los usuarios, saltándose el filtro: retirar una
            // plataforma dejaría sin origen las cuentas de otro, que no se ven desde aquí.
            var accountsUsingIt = await context.Accounts
                .IgnoreQueryFilters()
                .CountAsync(account => account.Platform == platformCode, token);

            platform.EnsureCanBeDeleted(accountsUsingIt);

            context.Platforms.Remove(platform);
            await context.SaveChangesAsync(token);

            return Results.NoContent();
        });

        var accounts = api.MapGroup("/accounts");

        accounts.MapGet("/", (IPortfolioQueries queries, CancellationToken token) =>
            queries.ListAccountsAsync(token));

        accounts.MapPost("/", async (
            CreateAccountRequest request,
            KapeaDbContext context,
            ICurrentUser user,
            CancellationToken token) =>
        {
            // La plataforma se valida contra el catálogo y no contra una lista escrita
            // en el código: es lo que hace que dar de alta un bróker de fichero baste
            // para poder abrirle una cuenta.
            if (!PlatformCode.TryParse(request.Platform, out var requested)
                || await context.Platforms.SingleOrDefaultAsync(entity => entity.Code == requested.Value, token) is not { } known)
            {
                var catalogue = await context.Platforms.Select(entity => entity.Name).ToListAsync(token);

                return Results.Problem(
                    $"Plataforma '{request.Platform}' no está en el catálogo. Disponibles: {string.Join(", ", catalogue)}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var platform = known.Code;

            var account = PlatformAccount.Create(
                user.Id, platform, request.Alias, Currency.FromCode(request.BaseCurrency));

            context.Accounts.Add(account);
            await context.SaveChangesAsync(token);

            return Results.Created(
                $"/api/accounts/{account.Id}",
                new AccountResponse(account.Id, platform.ToString(), account.Alias, account.BaseCurrency.Code));
        });

        accounts.MapDelete("/{accountId:guid}", async (
            Guid accountId,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            var account = await context.Accounts.SingleOrDefaultAsync(entity => entity.Id == accountId, token);

            if (account is null)
            {
                return Results.NotFound();
            }

            var transactions = await context.Transactions.CountAsync(entity => entity.AccountId == accountId, token);
            account.EnsureCanBeDeleted(transactions);

            context.Accounts.Remove(account);
            await context.SaveChangesAsync(token);

            return Results.NoContent();
        });
    }

    private static void MapCredentials(this RouteGroupBuilder api)
    {
        var credentials = api.MapGroup("/credentials");

        credentials.MapGet("/", (IPortfolioQueries queries, CancellationToken token) =>
            queries.ListCredentialsAsync(token));

        credentials.MapPost("/", async (
            RegisterBrokerCredentialRequest request,
            BrokerCredentialService service,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            if (!PlatformCode.TryParse(request.Platform, out var parsed))
            {
                return Results.Problem(
                    $"Plataforma '{request.Platform}' no soportada.", statusCode: StatusCodes.Status400BadRequest);
            }

            var platform = parsed.Value;

            // La cuenta se lee con el filtro por usuario puesto. Una cuenta ajena no
            // aparece, así que llega aquí como inexistente en lugar de como una cuenta
            // de otro sobre la que se podría escribir.
            var account = await context.Accounts
                .SingleOrDefaultAsync(entity => entity.Id == request.AccountId, token);

            if (account is null)
            {
                return Results.NotFound();
            }

            var credential = await service.RegisterAsync(
                account, platform, request.Alias, new ApiSecret(request.ApiKey, request.ApiSecret), token);

            context.BrokerCredentials.Add(credential);
            await context.SaveChangesAsync(token);

            return Results.Created($"/api/credentials/{credential.Id}", ToResponse(credential));
        });

        credentials.MapPut("/{credentialId:guid}", async (
            Guid credentialId,
            RotateBrokerCredentialRequest request,
            BrokerCredentialService service,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            var credential = await context.BrokerCredentials
                .SingleOrDefaultAsync(entity => entity.Id == credentialId, token);

            if (credential is null)
            {
                return Results.NotFound();
            }

            await service.RotateAsync(credential, new ApiSecret(request.ApiKey, request.ApiSecret), token);
            await context.SaveChangesAsync(token);

            return Results.Ok(ToResponse(credential));
        });

        credentials.MapDelete("/{credentialId:guid}", async (
            Guid credentialId,
            BrokerCredentialService service,
            KapeaDbContext context,
            CancellationToken token) =>
        {
            var credential = await context.BrokerCredentials
                .SingleOrDefaultAsync(entity => entity.Id == credentialId, token);

            if (credential is null)
            {
                return Results.NotFound();
            }

            await service.RevokeAsync(credential, token);
            await context.SaveChangesAsync(token);

            return Results.Ok(ToResponse(credential));
        });
    }

    private static void MapImports(this RouteGroupBuilder api)
    {
        var imports = api.MapGroup("/imports");

        imports.MapGet("/", (Guid? accountId, IPortfolioQueries queries, CancellationToken token) =>
            queries.ListImportRunsAsync(accountId, token));

        imports.MapGet("/{runId:guid}", async (Guid runId, IPortfolioQueries queries, CancellationToken token) =>
            await queries.FindImportRunAsync(runId, token) is { } run ? Results.Ok(run) : Results.NotFound());

        // Subida de fichero: normaliza y deja la vista previa preparada, sin persistir
        // ningún movimiento hasta que el usuario confirme.
        imports.MapPost("/file", async (
            Guid accountId,
            IFormFile file,
            KapeaDbContext context,
            IFileImporter importer,
            ImportPipeline pipeline,
            IPortfolioQueries queries,
            CancellationToken token) =>
        {
            if (file.Length > MaximumUploadBytes)
            {
                return Results.Problem(
                    $"El fichero supera el máximo admitido de {MaximumUploadBytes / (1024 * 1024)} MB.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var account = await context.Accounts.SingleOrDefaultAsync(entity => entity.Id == accountId, token);

            if (account is null)
            {
                return Results.NotFound();
            }

            // Una plataforma de API también admite fichero cuando hay un perfil que sepa
            // leerlo. Su API es la vía cómoda, pero puede no devolverlo todo, y entonces
            // el extracto que el usuario se descarga es la única forma de completarlo.
            var platform = await context.Platforms
                .SingleOrDefaultAsync(entity => entity.Code == account.Platform, token);

            if (platform is not null
                && platform.ImportKind != PlatformImportKind.File
                && !await context.ImportProfiles.AnyAsync(profile => profile.Platform == account.Platform, token))
            {
                return Results.Problem(
                    $"Los movimientos de {platform.Name} llegan por su API y no hay ningún perfil que sepa leer sus ficheros.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // El fichero se copia a memoria porque hay que leerlo dos veces: una para
            // ver sus cabeceras y elegir el perfil, otra con el delimitador que ese
            // perfil declara. El flujo de la petición no se puede rebobinar.
            using var content = new MemoryStream();
            await using (var upload = file.OpenReadStream())
            {
                await upload.CopyToAsync(content, token);
            }

            content.Position = 0;

            var imported = await importer.ReadAsync(account.Platform, content, file.FileName, token);
            var run = await pipeline.StageAsync(
                accountId, imported.Read, file.FileName, token, imported.ProfileId, imported.ProfileVersion);

            return Results.Ok(new ImportPreviewResponse(
                (await queries.FindImportRunAsync(run.Id, token))!, imported.Read.Warnings));
        }).DisableAntiforgery();

        imports.MapPost("/{runId:guid}/confirm", async (
            Guid runId,
            ImportPipeline pipeline,
            InternalTransferService transfers,
            PortfolioCalculationService calculation,
            IPortfolioQueries queries,
            CancellationToken token) =>
        {
            await pipeline.ConfirmAsync(runId, cancellationToken: token);

            // Justo después de importar es cuando pueden aparecer las dos patas de un
            // traspaso, así que se buscan aquí. Solo se proponen: hasta que el usuario
            // decida, sus movimientos quedan fuera del cálculo.
            await transfers.ProposeAsync(cancellationToken: token);
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.Ok(await queries.FindImportRunAsync(runId, token));
        });

        imports.MapPost("/{runId:guid}/discard", async (
            Guid runId, ImportPipeline pipeline, CancellationToken token) =>
        {
            await pipeline.DiscardAsync(runId, token);

            return Results.NoContent();
        });

        imports.MapDelete("/{runId:guid}", async (
            Guid runId,
            ImportPipeline pipeline,
            KapeaDbContext context,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            // Las dependencias se calculan aquí y no en el motor: son las que conoce la
            // base de datos, no algo que el motor pueda deducir de los movimientos.
            await pipeline.DeleteAsync(
                runId,
                transactions => Dependencies(context, transactions),
                token);

            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.NoContent();
        });
    }

    /// <summary>Cuántos días atrás se enseña la evolución si nadie dice otra cosa.</summary>
    private const int DefaultHistoryDays = 365;

    /// <summary>Ventana de los indicadores por omisión. Veinte sesiones es el mes bursátil.</summary>
    private const int DefaultIndicatorWindow = 20;

    /// <summary>
    /// El periodo que se consulta.
    /// </summary>
    /// <remarks>
    /// Sin fechas se enseña el último año, que es lo que casi siempre se quiere mirar y
    /// lo que evita que una cartera de años cargue toda su historia sin pedirlo.
    /// </remarks>
    private static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to, TimeProvider time)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var hasta = to ?? today;

        return (from ?? hasta.AddDays(-DefaultHistoryDays), hasta);
    }

    private static void MapPortfolio(this RouteGroupBuilder api)
    {
        api.MapGet("/portfolio/history", (
            DateOnly? from,
            DateOnly? to,
            IPortfolioQueries queries,
            TimeProvider time,
            CancellationToken token) =>
        {
            var (desde, hasta) = Range(from, to, time);

            return queries.GetHistoryAsync(desde, hasta, token);
        });

        api.MapGet("/portfolio/performance", (
            DateOnly? from,
            DateOnly? to,
            Guid? benchmark,
            IPortfolioQueries queries,
            TimeProvider time,
            CancellationToken token) =>
        {
            var (desde, hasta) = Range(from, to, time);

            return queries.GetPerformanceAsync(desde, hasta, benchmark, token);
        });

        api.MapGet("/portfolio/history/{assetId:guid}", async (
            Guid assetId,
            DateOnly? from,
            DateOnly? to,
            int? window,
            IPortfolioQueries queries,
            TimeProvider time,
            CancellationToken token) =>
        {
            var (desde, hasta) = Range(from, to, time);

            return await queries.GetAssetHistoryAsync(
                assetId, desde, hasta, window ?? DefaultIndicatorWindow, token) is { } history
                ? Results.Ok(history)
                : Results.NotFound();
        });

        api.MapGet("/transactions", (
            Guid? accountId, bool? requiresReview, IPortfolioQueries queries, CancellationToken token) =>
            queries.ListTransactionsAsync(accountId, requiresReview ?? false, token));

        // Búsqueda paginada. Un histórico de cripto son miles de apuntes, y traerlos
        // todos para enseñar veinte deja la pantalla en blanco mientras llegan.
        api.MapGet("/transactions/search", (
            Guid? accountId,
            string? asset,
            string? type,
            int? year,
            bool? requiresReview,
            string? search,
            int? page,
            int? pageSize,
            IPortfolioQueries queries,
            CancellationToken token) =>
            queries.SearchTransactionsAsync(
                new TransactionQuery(
                    accountId, asset, type, year, requiresReview ?? false, search, page ?? 1, pageSize ?? 50),
                token));

        api.MapGet("/portfolio", (IPortfolioQueries queries, CancellationToken token) =>
            queries.GetPortfolioAsync(token));

        api.MapGet("/results/{taxYear:int}", (int taxYear, IPortfolioQueries queries, CancellationToken token) =>
            queries.GetTaxYearResultsAsync(taxYear, token));

        api.MapGet("/results/detail/{disposalTransactionId:guid}", async (
            Guid disposalTransactionId, IPortfolioQueries queries, CancellationToken token) =>
            await queries.FindRealizedResultAsync(disposalTransactionId, token) is { } result
                ? Results.Ok(result)
                : Results.NotFound());

        // Sincronización a petición. La programada corre cada pocas horas, y esperar a
        // que toque después de dar de alta una credencial no tiene por qué.
        api.MapPost("/sync", async (
            bool? full,
            Kapea.Application.Synchronization.SynchronizationService synchronization,
            InternalTransferService transfers,
            PortfolioCalculationService calculation,
            ICurrentUser user,
            CancellationToken token) =>
        {
            // Solo las cuentas de quien la pide: lanzarla no puede servir para mover los
            // datos de otro.
            // Con «full» se relee el histórico entero. Hace falta cuando se corrige cómo
            // se interpreta un movimiento: lo ya importado se descarta por duplicado, así
            // que solo entra lo que antes no se sabía leer.
            var report = await synchronization.RunAsync(user.Id, full ?? false, token);

            if (report.Results.Any(result => result.ImportedRecords > 0))
            {
                await transfers.ProposeAsync(cancellationToken: token);
                await calculation.RecalculateAsync(cancellationToken: token);
            }

            return Results.Ok(new SynchronizationResponse(
                report.Results.Count,
                report.ImportedAccounts,
                report.FailedAccounts,
                report.Results.Sum(result => result.ImportedRecords),
                [.. report.Results
                    .Where(result => result.Detail is { Length: > 0 })
                    .Select(result => $"{result.Platform}: {result.Detail}")]));
        });

        // Relee lo que quedó sin clasificar con las reglas de hoy. No pide nada a la
        // plataforma: cada movimiento guarda el texto con el que entró.
        api.MapPost("/transactions/reinterpret", async (
            TransactionReinterpretationService reinterpretation,
            PortfolioCalculationService calculation,
            CancellationToken token) =>
        {
            var result = await reinterpretation.ReinterpretAsync(token);

            // Un movimiento sin clasificar está fuera del cálculo. En cuanto pasa a
            // significar algo, la cartera cambia y hay que rehacerla.
            if (result.Reclassified > 0)
            {
                await calculation.RecalculateAsync(cancellationToken: token);
            }

            return Results.Ok(new ReinterpretationResponse(
                result.Reclassified, result.StillUnknown, result.NotSupported));
        });

        api.MapPost("/portfolio/recalculate", async (
            PortfolioCalculationService calculation, IPortfolioQueries queries, CancellationToken token) =>
        {
            await calculation.RecalculateAsync(cancellationToken: token);

            return Results.Ok(await queries.GetPortfolioAsync(token));
        });
    }

    private static void MapTransfers(this RouteGroupBuilder api)
    {
        var transfers = api.MapGroup("/transfers");

        transfers.MapGet("/", (bool? onlyPending, IPortfolioQueries queries, CancellationToken token) =>
            queries.ListTransfersAsync(onlyPending ?? true, token));

        transfers.MapPost("/{transferId:guid}/confirm", (
                Guid transferId, InternalTransferService service, PortfolioCalculationService calculation,
                CancellationToken token) =>
            ResolveAsync(transferId, service, calculation, token, confirm: true));

        transfers.MapPost("/{transferId:guid}/reject", (
                Guid transferId, InternalTransferService service, PortfolioCalculationService calculation,
                CancellationToken token) =>
            ResolveAsync(transferId, service, calculation, token, confirm: false));

        transfers.MapPost("/detect", async (
            InternalTransferService service, IPortfolioQueries queries, CancellationToken token) =>
        {
            await service.ProposeAsync(cancellationToken: token);

            return Results.Ok(await queries.ListTransfersAsync(onlyPending: true, token));
        });
    }

    private static async Task<IResult> ResolveAsync(
        Guid transferId,
        InternalTransferService service,
        PortfolioCalculationService calculation,
        CancellationToken token,
        bool confirm)
    {
        var assetId = await service.ResolveAsync(transferId, confirm, token);

        if (assetId is null)
        {
            return Results.NotFound();
        }

        // Se recalcula solo el activo afectado: confirmar un traspaso mueve sus lotes,
        // y el resto de la cartera no cambia.
        await calculation.RecalculateAsync(assetId, token);

        return Results.NoContent();
    }

    private static IReadOnlyList<string> Dependencies(
        KapeaDbContext context,
        IReadOnlyList<Domain.Transactions.Transaction> transactions)
    {
        var ids = transactions.Select(transaction => transaction.Id).ToHashSet();

        return
        [
            .. context.InternalTransfers
                .Where(transfer => transfer.Status == InternalTransferStatus.Confirmed)
                .AsEnumerable()
                .Where(transfer => ids.Contains(transfer.OutgoingTransactionId)
                    || ids.Contains(transfer.IncomingTransactionId))
                .Select(transfer => $"el traspaso confirmado {transfer.Id} usa uno de sus movimientos"),
        ];
    }

    private static BrokerCredentialResponse ToResponse(Domain.Credentials.BrokerCredential credential) =>
        new(
            credential.Id, credential.AccountId, credential.Platform.ToString(), credential.Alias,
            credential.Scopes.ToString(), credential.Status.ToString(), credential.CreatedAt,
            credential.RotatedAt, credential.LastSynchronizedAt, credential.InvalidReason);
}
