using Kapea.Application.Abstractions;
using Kapea.Application.Portfolio;
using Kapea.Domain.Calculation;
using Kapea.Domain.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>Lecturas de la cartera sobre EF Core, ya en la forma que consume el cliente.</summary>
public sealed class PortfolioQueries(KapeaDbContext context, IMarketPriceProvider prices) : IPortfolioQueries
{
    public async Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(CancellationToken cancellationToken = default) =>
        await context.Accounts
            .OrderBy(account => account.Alias)
            .Select(account => new AccountResponse(
                account.Id, account.Platform.ToString(), account.Alias, account.BaseCurrency.Code))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<BrokerCredentialResponse>> ListCredentialsAsync(
        CancellationToken cancellationToken = default) =>
        await context.BrokerCredentials
            .OrderBy(credential => credential.Alias)
            .Select(credential => new BrokerCredentialResponse(
                credential.Id,
                credential.AccountId,
                credential.Platform.ToString(),
                credential.Alias,
                credential.Scopes.ToString(),
                credential.Status.ToString(),
                credential.CreatedAt,
                credential.RotatedAt,
                credential.LastSynchronizedAt,
                credential.InvalidReason))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ImportRunResponse>> ListImportRunsAsync(
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var runs = await context.ImportRuns
            .Include(run => run.Records)
            .Where(run => accountId == null || run.AccountId == accountId)
            .OrderByDescending(run => run.StartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. runs.Select(ToResponse)];
    }

    public async Task<ImportRunResponse?> FindImportRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await context.ImportRuns
            .Include(stored => stored.Records)
            .SingleOrDefaultAsync(stored => stored.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        return run is null ? null : ToResponse(run);
    }

    public async Task<IReadOnlyList<TransactionResponse>> ListTransactionsAsync(
        Guid? accountId,
        bool onlyRequiringReview,
        CancellationToken cancellationToken = default)
    {
        var transactions = await context.Transactions
            .Where(transaction => accountId == null || transaction.AccountId == accountId)
            .Where(transaction => !onlyRequiringReview || transaction.Type == TransactionType.Unknown)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var symbols = await SymbolsAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. transactions
                .OrderByDescending(transaction => transaction.OccurredAt.Instant)
                .Select(transaction => new TransactionResponse(
                    transaction.Id,
                    transaction.AccountId,
                    transaction.AssetId,
                    transaction.AssetId is { } id ? symbols.GetValueOrDefault(id) : null,
                    transaction.Type.ToString(),
                    transaction.Quantity.Value,
                    transaction.UnitPrice?.Amount,
                    transaction.GrossAmount.Amount,
                    transaction.Currency.Code,
                    transaction.Fee.Amount,
                    transaction.OccurredAt.Instant,
                    transaction.OccurredAt.SourceTimeZoneId,
                    transaction.Origin.ToString(),
                    transaction.RequiresReview,
                    transaction.Source.ImportRunId,
                    transaction.Source.NaturalId,
                    transaction.Source.RowNumber,
                    transaction.Source.RawContent,
                    transaction.AppliedExchangeRate?.UnitsPerEuro,
                    transaction.AppliedExchangeRate?.RateDate,
                    transaction.AppliedExchangeRate?.WasSubstituted ?? false)),
        ];
    }

    public async Task<PortfolioResponse> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var lots = await context.Lots.ToListAsync(cancellationToken).ConfigureAwait(false);
        var assets = await context.Assets.ToDictionaryAsync(asset => asset.Id, cancellationToken).ConfigureAwait(false);

        var open = lots.Where(lot => !lot.IsExhausted).GroupBy(lot => lot.AssetId).ToList();
        var symbols = open
            .Where(group => assets.ContainsKey(group.Key))
            .Select(group => assets[group.Key].CanonicalSymbol)
            .ToList();

        var quotes = await prices.GetPricesAsync(symbols, cancellationToken).ConfigureAwait(false);
        var positions = new List<OpenPositionResponse>();

        foreach (var group in open)
        {
            var asset = assets.GetValueOrDefault(group.Key);
            var symbol = asset?.CanonicalSymbol ?? group.Key.ToString();
            var quote = quotes.GetValueOrDefault(symbol);

            var position = OpenPosition.From(
                group.Key,
                group,
                quote is null ? null : Domain.ValueObjects.Money.Euros(quote.PriceInEuros),
                quote?.AsOf);

            if (position is null)
            {
                continue;
            }

            positions.Add(new OpenPositionResponse(
                position.AssetId,
                symbol,
                asset?.IsVerified ?? false,
                position.Quantity.Value,
                position.CostInEuros.Amount,
                position.AverageCostInEuros.Amount,
                position.MarketPriceInEuros?.Amount,
                position.MarketValueInEuros?.Amount,
                position.UnrealisedResultInEuros?.Amount,
                position.PriceAsOf));
        }

        var unclassified = await context.Transactions
            .CountAsync(transaction => transaction.Type == TransactionType.Unknown, cancellationToken)
            .ConfigureAwait(false);

        var pendingTransfers = await context.InternalTransfers
            .CountAsync(transfer => transfer.Status == InternalTransferStatus.Proposed, cancellationToken)
            .ConfigureAwait(false);

        return new PortfolioResponse(
            [.. positions.OrderBy(position => position.AssetSymbol)],
            positions.Sum(position => position.CostInEuros),
            positions.All(position => position.MarketValueInEuros is not null)
                ? positions.Sum(position => position.MarketValueInEuros!.Value)
                : null,
            unclassified,
            pendingTransfers,
            []);
    }

    public async Task<TaxYearResultsResponse> GetTaxYearResultsAsync(
        int taxYear,
        CancellationToken cancellationToken = default)
    {
        var results = await context.RealizedResults
            .Include(result => result.ConsumedLots)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // El ejercicio se filtra en memoria: se decide por la fecha en la zona del
        // origen, y esa conversión no se puede traducir a SQL.
        var ofYear = results.Where(result => result.TaxYear == taxYear).ToList();
        var symbols = await SymbolsAsync(cancellationToken).ConfigureAwait(false);

        var details = ofYear
            .OrderBy(result => result.DisposedAt.Instant)
            .Select(result => ToResponse(result, symbols))
            .ToList();

        var byAsset = ofYear
            .GroupBy(result => result.AssetId)
            .Select(group => new AssetResultResponse(
                group.Key,
                symbols.GetValueOrDefault(group.Key, group.Key.ToString()),
                group.Sum(result => result.ProceedsInEuros.Amount),
                group.Sum(result => result.AcquisitionCostInEuros.Amount),
                group.Sum(result => result.ResultInEuros.Amount)))
            .OrderBy(entry => entry.AssetSymbol)
            .ToList();

        var unclassified = await context.Transactions
            .CountAsync(transaction => transaction.Type == TransactionType.Unknown, cancellationToken)
            .ConfigureAwait(false);

        return new TaxYearResultsResponse(
            taxYear,
            byAsset,
            byAsset.Sum(entry => entry.ResultInEuros),
            details,
            unclassified == 0);
    }

    public async Task<RealizedResultResponse?> FindRealizedResultAsync(
        Guid disposalTransactionId,
        CancellationToken cancellationToken = default)
    {
        var result = await context.RealizedResults
            .Include(stored => stored.ConsumedLots)
            .SingleOrDefaultAsync(stored => stored.DisposalTransactionId == disposalTransactionId, cancellationToken)
            .ConfigureAwait(false);

        return result is null
            ? null
            : ToResponse(result, await SymbolsAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<InternalTransferResponse>> ListTransfersAsync(
        bool onlyPending,
        CancellationToken cancellationToken = default)
    {
        var transfers = await context.InternalTransfers
            .Where(transfer => !onlyPending || transfer.Status == InternalTransferStatus.Proposed)
            .OrderByDescending(transfer => transfer.ProposedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var symbols = await SymbolsAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. transfers.Select(transfer => new InternalTransferResponse(
                transfer.Id,
                transfer.AssetId,
                symbols.GetValueOrDefault(transfer.AssetId, transfer.AssetId.ToString()),
                transfer.SourceAccountId,
                transfer.DestinationAccountId,
                transfer.SentQuantity.Value,
                transfer.ReceivedQuantity.Value,
                transfer.NetworkFeeQuantity.Value,
                transfer.Status.ToString(),
                transfer.ProposedAt)),
        ];
    }

    private async Task<Dictionary<Guid, string>> SymbolsAsync(CancellationToken cancellationToken) =>
        await context.Assets
            .ToDictionaryAsync(asset => asset.Id, asset => asset.CanonicalSymbol, cancellationToken)
            .ConfigureAwait(false);

    private static RealizedResultResponse ToResponse(RealizedResult result, Dictionary<Guid, string> symbols) =>
        new(
            result.DisposalTransactionId,
            result.AssetId,
            symbols.GetValueOrDefault(result.AssetId, result.AssetId.ToString()),
            result.DisposedAt.Instant,
            result.TaxYear,
            result.Quantity.Value,
            result.ProceedsInEuros.Amount,
            result.AcquisitionCostInEuros.Amount,
            result.ResultInEuros.Amount,
            [
                .. result.ConsumedLots
                    .OrderBy(lot => lot.AcquiredAt.Instant)
                    .Select(lot => new ConsumedLotResponse(
                        lot.LotId,
                        lot.AcquisitionTransactionId,
                        lot.AcquiredAt.Instant,
                        lot.Quantity.Value,
                        lot.AcquisitionCostInEuros.Amount,
                        lot.ProceedsInEuros.Amount,
                        lot.ResultInEuros.Amount)),
            ]);

    private static ImportRunResponse ToResponse(ImportRun run) =>
        new(
            run.Id,
            run.AccountId,
            run.Platform.ToString(),
            run.FileName,
            run.Status.ToString(),
            run.StartedAt,
            run.CompletedAt,
            run.RecordsRead,
            run.RecordsImported,
            run.DuplicatesDiscarded,
            run.RecordsRejected,
            run.NonFinancialRecords,
            run.FailureReason,
            [
                .. run.Records
                    .Where(record => record.Outcome == StagedRecordOutcome.Rejected)
                    .OrderBy(record => record.RowNumber)
                    .Select(record => new RejectedRecordResponse(
                        record.Id,
                        record.RowNumber,
                        record.NaturalId,
                        record.RawContent,
                        record.RejectionReason ?? "Sin motivo indicado.")),
            ]);
}
