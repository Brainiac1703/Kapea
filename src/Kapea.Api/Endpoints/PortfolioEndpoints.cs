using Kapea.Application.Credentials;
using Kapea.Application.Import;
using Kapea.Application.Portfolio;
using Kapea.Application.Abstractions;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.Transfers;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Import.Xtb;
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
        api.MapCredentials();
        api.MapImports();
        api.MapPortfolio();
        api.MapTransfers();
    }

    private static void MapAccounts(this RouteGroupBuilder api)
    {
        var accounts = api.MapGroup("/accounts");

        accounts.MapGet("/", (IPortfolioQueries queries, CancellationToken token) =>
            queries.ListAccountsAsync(token));

        accounts.MapPost("/", async (
            CreateAccountRequest request,
            KapeaDbContext context,
            ICurrentUser user,
            CancellationToken token) =>
        {
            if (!Enum.TryParse<Platform>(request.Platform, ignoreCase: true, out var platform))
            {
                return Results.Problem(
                    $"Plataforma '{request.Platform}' no soportada. Soportadas: {string.Join(", ", Enum.GetNames<Platform>())}.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

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
            if (!Enum.TryParse<Platform>(request.Platform, ignoreCase: true, out var platform))
            {
                return Results.Problem(
                    $"Plataforma '{request.Platform}' no soportada.", statusCode: StatusCodes.Status400BadRequest);
            }

            var credential = await service.RegisterAsync(
                request.AccountId, platform, request.Alias, new ApiSecret(request.ApiKey, request.ApiSecret), token);

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
            IImportAdapterRegistry adapters,
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

            var adapter = adapters.GetFileAdapter(account.Platform);

            await using var content = file.OpenReadStream();
            var read = await adapter.ReadAsync(content, file.FileName, token);
            var run = await pipeline.StageAsync(accountId, read, file.FileName, token);

            return Results.Ok(new ImportPreviewResponse(
                (await queries.FindImportRunAsync(run.Id, token))!, read.Warnings));
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

    private static void MapPortfolio(this RouteGroupBuilder api)
    {
        api.MapGet("/transactions", (
            Guid? accountId, bool? requiresReview, IPortfolioQueries queries, CancellationToken token) =>
            queries.ListTransactionsAsync(accountId, requiresReview ?? false, token));

        api.MapGet("/portfolio", (IPortfolioQueries queries, CancellationToken token) =>
            queries.GetPortfolioAsync(token));

        api.MapGet("/results/{taxYear:int}", (int taxYear, IPortfolioQueries queries, CancellationToken token) =>
            queries.GetTaxYearResultsAsync(taxYear, token));

        api.MapGet("/results/detail/{disposalTransactionId:guid}", async (
            Guid disposalTransactionId, IPortfolioQueries queries, CancellationToken token) =>
            await queries.FindRealizedResultAsync(disposalTransactionId, token) is { } result
                ? Results.Ok(result)
                : Results.NotFound());

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
