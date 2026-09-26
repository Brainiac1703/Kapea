using Kapea.Application.Abstractions;
using Kapea.Application.Import;
using Kapea.Application.Portfolio;
using Kapea.Domain.Calculation;
using Kapea.Domain.Indicators;
using Kapea.Domain.Performance;
using Kapea.Domain.ValueObjects;
using Kapea.Domain.Exchange;
using Kapea.Domain.Assets;
using Kapea.Domain.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.Transfers;
using Kapea.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence.Stores;

/// <summary>Lecturas de la cartera sobre EF Core, ya en la forma que consume el cliente.</summary>
public sealed class PortfolioQueries(
    KapeaDbContext context,
    IMarketPriceProvider prices,
    IExchangeRateProvider exchangeRates,
    IPriceHistoryStore priceHistory) : IPortfolioQueries
{
    /// <summary>Cuántas posiciones se miran al avisar de concentración.</summary>
    private const int TopPositions = 3;

    /// <summary>A partir de cuánto entre las tres mayores se avisa.</summary>
    private const decimal ConcentrationThreshold = 0.7m;

    /// <summary>Tope por posición a partir del cual se señala.</summary>
    private const decimal PositionCap = 0.25m;

    // El catálogo es de la instalación, no de cada usuario, así que se salta el filtro
    // global: sin esto no devolvería nada, porque las plataformas no tienen dueño.
    public async Task<IReadOnlyList<PlatformResponse>> ListPlatformsAsync(
        CancellationToken cancellationToken = default) =>
        await context.Platforms
            .OrderBy(platform => platform.Name)
            .Select(platform => new PlatformResponse(
                platform.Code.Value, platform.Name, platform.ImportKind.ToString(), platform.BuiltIn))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

        // Los nombres de perfil se resuelven de una vez: son unos pocos, y consultarlos
        // por ejecución multiplicaría las idas a la base de datos.
        var profileNames = await context.ImportProfiles
            .ToDictionaryAsync(profile => profile.Id, profile => profile.Name, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. runs.Select(run => ToResponse(
                run,
                run.ProfileId is { } profileId ? profileNames.GetValueOrDefault(profileId) : null)),
        ];
    }

    public async Task<ImportRunResponse?> FindImportRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await context.ImportRuns
            .Include(stored => stored.Records)
            .SingleOrDefaultAsync(stored => stored.Id == runId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return null;
        }

        var response = ToResponse(run, await ProfileNameAsync(run, cancellationToken).ConfigureAwait(false));

        return run.Status == Domain.Import.ImportRunStatus.Staged
            ? response with { ManualMatches = await ManualMatchesAsync(run, cancellationToken).ConfigureAwait(false) }
            : response;
    }

    /// <summary>
    /// Filas de una importación pendiente que coinciden con un apunte manual de su cuenta.
    /// </summary>
    /// <remarks>
    /// Todas las que entrarían, no sólo las de la muestra: la coincidencia puede estar en la
    /// fila quinientos. El símbolo de la fila se traduce al activo del catálogo para
    /// compararlo con el del apunte.
    /// </remarks>
    private async Task<IReadOnlyList<ManualMatchResponse>> ManualMatchesAsync(ImportRun run, CancellationToken cancellationToken)
    {
        var manuals = await context.Transactions
            .Where(transaction => transaction.Origin == TransactionOrigin.Manual && transaction.AccountId == run.AccountId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (manuals.Count == 0)
        {
            return [];
        }

        var assetsBySymbol = await context.Assets
            .ToDictionaryAsync(asset => asset.CanonicalSymbol, asset => asset.Id, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var matches = new List<ManualMatchResponse>();

        foreach (var staged in run.Records.Where(record => record.Outcome == StagedRecordOutcome.Importable))
        {
            var record = StagedRecordReader.Read(staged.Payload, staged.RawContent);
            var occurredAt = record.ToOccurrence();
            var day = DateOnly.FromDateTime(occurredAt.InSourceTimeZone.DateTime);
            Guid? assetId = record.AssetSymbol is { } symbol && assetsBySymbol.TryGetValue(symbol, out var found) ? found : null;

            if (record.AssetSymbol is not null && assetId is null)
            {
                continue;
            }

            var manual = manuals.FirstOrDefault(candidate => ManualCoincidence.Matches(
                candidate, run.AccountId, record.Type, assetId, new Quantity(record.Quantity), day));

            if (manual is not null)
            {
                matches.Add(new ManualMatchResponse(
                    staged.RowNumber,
                    occurredAt.Instant,
                    record.Type.ToString(),
                    record.AssetSymbol,
                    record.Quantity,
                    manual.Id,
                    manual.OccurredAt.Instant,
                    manual.Note));
            }
        }

        return matches;
    }

    public async Task<TransactionPageResponse> SearchTransactionsAsync(
        TransactionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var symbols = await SymbolsAsync(cancellationToken).ConfigureAwait(false);

        var assetIds = query.AssetSymbol is { Length: > 0 } wanted
            ? symbols
                .Where(entry => entry.Value.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Key)
                .ToHashSet()
            : null;

        var fileNames = await ImportFileNamesAsync(cancellationToken).ConfigureAwait(false);

        // Con los anulados: la lista de movimientos es donde se ven, marcados, y desde
        // donde se deshace la anulación. El filtro de estado decide si se quieren.
        var filtered = context.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .Where(transaction => query.AccountId == null || transaction.AccountId == query.AccountId)
            .Where(transaction => !query.OnlyRequiringReview || transaction.Type == TransactionType.Unknown)
            .Where(transaction => query.Year == null || transaction.OccurredAt.Instant.Year == query.Year);

        // Un tipo que no se reconoce se descarta en lugar de vaciar la lista: pedir
        // «Buy» y una errata no puede esconder las compras.
        var requestedTypes = (query.Types ?? [])
            .Select(name => Enum.TryParse<TransactionType>(name, ignoreCase: true, out var parsed)
                ? parsed
                : (TransactionType?)null)
            .Where(parsed => parsed is not null)
            .Select(parsed => parsed!.Value)
            .Distinct()
            .ToList();

        if (requestedTypes.Count > 0)
        {
            filtered = filtered.Where(transaction => requestedTypes.Contains(transaction.Type));
        }

        if (query.Voided is { } voided)
        {
            filtered = voided
                ? filtered.Where(transaction => transaction.VoidedAt != null)
                : filtered.Where(transaction => transaction.VoidedAt == null);
        }

        filtered = query.Origin switch
        {
            MovementOrigins.Manual => filtered.Where(transaction => transaction.Origin == TransactionOrigin.Manual),
            MovementOrigins.Adjustment => filtered.Where(transaction => transaction.Origin == TransactionOrigin.ManualAdjustment),
            MovementOrigins.File => ImportedFrom(filtered, [.. fileNames.Keys]),
            MovementOrigins.Api => filtered.Where(transaction => transaction.Origin == TransactionOrigin.Imported
                && (transaction.Source.ImportRunId == null || !fileNames.Keys.Contains(transaction.Source.ImportRunId.Value))),
            _ => filtered,
        };

        if (assetIds is not null)
        {
            filtered = filtered.Where(transaction =>
                transaction.AssetId != null && assetIds.Contains(transaction.AssetId.Value));
        }

        if (query.Search is { Length: > 0 } search)
        {
            filtered = filtered.Where(transaction =>
                transaction.Source.RawContent != null && transaction.Source.RawContent.Contains(search));
        }

        var total = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);

        // Los años y los tipos salen de todo el histórico y no de la página: un
        // desplegable que solo ofreciera lo que ya se ve no serviría para llegar a lo
        // que no se ve.
        var years = await context.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .Select(transaction => transaction.OccurredAt.Instant.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var types = await context.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .Select(transaction => transaction.Type)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await filtered
            .OrderByDescending(transaction => transaction.OccurredAt.Instant)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var profileNames = await context.ImportProfiles
            .ToDictionaryAsync(profile => profile.Id, profile => profile.Name, cancellationToken)
            .ConfigureAwait(false);

        return new TransactionPageResponse(
            [.. items.Select(transaction => ToResponse(transaction, symbols, profileNames, fileNames))],
            total,
            page,
            size,
            [.. types.Select(entry => entry.ToString()).Order(StringComparer.Ordinal)],
            years);
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

        // El nombre del perfil se resuelve aquí y no fila a fila: son unos pocos, y
        // consultarlos por movimiento multiplicaría las idas a la base de datos.
        var profileNames = await context.ImportProfiles
            .ToDictionaryAsync(profile => profile.Id, profile => profile.Name, cancellationToken)
            .ConfigureAwait(false);

        var fileNames = await ImportFileNamesAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. transactions
                .OrderByDescending(transaction => transaction.OccurredAt.Instant)
                .Select(transaction => ToResponse(transaction, symbols, profileNames, fileNames)),
        ];
    }

    /// <summary>
    /// Nombre del fichero de cada importación que vino de un fichero.
    /// </summary>
    /// <remarks>
    /// Es lo que separa un importado por fichero de uno por API: el dominio no lo distingue
    /// porque para el cálculo da igual, pero quien revisa una cifra quiere saberlo.
    /// </remarks>
    private async Task<Dictionary<Guid, string>> ImportFileNamesAsync(CancellationToken cancellationToken) =>
        await context.ImportRuns
            .Where(run => run.FileName != null)
            .ToDictionaryAsync(run => run.Id, run => run.FileName!, cancellationToken)
            .ConfigureAwait(false);

    private static IQueryable<Transaction> ImportedFrom(IQueryable<Transaction> transactions, List<Guid> fileRuns) =>
        transactions.Where(transaction => transaction.Origin == TransactionOrigin.Imported
            && transaction.Source.ImportRunId != null
            && fileRuns.Contains(transaction.Source.ImportRunId.Value));

    private static string OriginOf(Transaction transaction, IReadOnlyDictionary<Guid, string> fileNames) =>
        transaction.Origin switch
        {
            TransactionOrigin.Manual => MovementOrigins.Manual,
            TransactionOrigin.ManualAdjustment => MovementOrigins.Adjustment,
            _ when transaction.Source.ImportRunId is { } run && fileNames.ContainsKey(run) => MovementOrigins.File,
            _ => MovementOrigins.Api,
        };

    /// <summary>Un movimiento con su rastro hasta el origen, en la forma que lee el cliente.</summary>
    private static TransactionResponse ToResponse(
        Transaction transaction,
        IReadOnlyDictionary<Guid, string> symbols,
        IReadOnlyDictionary<Guid, string> profileNames,
        IReadOnlyDictionary<Guid, string> fileNames) =>
        new(
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
                    OriginOf(transaction, fileNames),
                    transaction.RequiresReview,
                    transaction.Source.ImportRunId,
                    transaction.Source.NaturalId,
                    transaction.Source.RowNumber,
                    transaction.Source.RawContent,
                    transaction.AppliedExchangeRate?.UnitsPerEuro,
                    transaction.AppliedExchangeRate?.RateDate,
                    transaction.AppliedExchangeRate?.WasSubstituted ?? false,
                    transaction.Source.ProfileId,
                    transaction.Source.ProfileId is { } profileId
                        ? profileNames.GetValueOrDefault(profileId)
                        : null,
                    transaction.Source.ProfileVersion,
                    transaction.Note,
                    transaction.RegisteredAt,
                    transaction.RevisedAt,
                    transaction.AdjustmentReason,
                    transaction.VoidedAt,
                    transaction.VoidReason,
                    transaction.Source.ImportRunId is { } runId ? fileNames.GetValueOrDefault(runId) : null,
                    transaction.AmountIsEstimated,

                    // Qué suma y qué resta lo decide el dominio: la pantalla sólo lo
                    // pinta. Y no es el efecto en caja, que sólo mira los euros: una
                    // recompensa cobrada en cripto no mueve un euro y sin embargo suma.
                    MovementDirections.Of(transaction).ToString());

    /// <summary>
    /// La cartera entera: posiciones agrupadas por clase, efectivo, patrimonio,
    /// resultado acumulado y rendimientos cobrados.
    /// </summary>
    /// <remarks>
    /// Las cifras las compone el dominio; aquí solo se reúne lo que necesita. Así las
    /// mismas reglas valen desde cualquier sitio y se pueden comprobar sin base de datos.
    /// </remarks>
    public async Task<PortfolioResponse> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var assets = await context.Assets.ToDictionaryAsync(asset => asset.Id, cancellationToken).ConfigureAwait(false);
        var lots = await context.Lots.ToListAsync(cancellationToken).ConfigureAwait(false);
        var transactions = await context.Transactions.ToListAsync(cancellationToken).ConfigureAwait(false);
        var transfers = await context.InternalTransfers.ToListAsync(cancellationToken).ConfigureAwait(false);
        var realized = await context.RealizedResults.ToListAsync(cancellationToken).ConfigureAwait(false);
        var incomes = await context.CapitalIncomes.ToListAsync(cancellationToken).ConfigureAwait(false);
        var inconsistencies = await context.CalculationInconsistencies.ToListAsync(cancellationToken).ConfigureAwait(false);

        var open = lots.Where(lot => !lot.IsExhausted).GroupBy(lot => lot.AssetId).ToList();

        var quotes = await prices
            .GetPricesAsync(
                [.. open.Where(group => assets.ContainsKey(group.Key)).Select(group => assets[group.Key].CanonicalSymbol)],
                cancellationToken)
            .ConfigureAwait(false);

        var feesByAsset = transactions
            .Where(transaction => transaction.AssetId is not null && !transaction.RequiresReview)
            .GroupBy(transaction => transaction.AssetId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Aggregate(Money.Euros(0m), (total, transaction) => total + transaction.FeeInEuros));

        var realizedByAsset = realized
            .GroupBy(result => result.AssetId)
            .ToDictionary(
                group => group.Key,
                group => group.Aggregate(Money.Euros(0m), (total, result) => total + result.ResultInEuros));

        var portfolioAssets = new List<PortfolioAsset>();
        var responsesByAsset = new Dictionary<Guid, (string Symbol, bool Verified)>();

        foreach (var group in open.OrderBy(group => assets.GetValueOrDefault(group.Key)?.CanonicalSymbol))
        {
            var asset = assets.GetValueOrDefault(group.Key);
            var symbol = asset?.CanonicalSymbol ?? group.Key.ToString();
            var quote = quotes.GetValueOrDefault(symbol);

            var position = OpenPosition.From(
                group.Key,
                group,
                quote is null ? null : Money.Euros(quote.PriceInEuros),
                quote?.AsOf);

            if (position is null)
            {
                continue;
            }

            portfolioAssets.Add(new PortfolioAsset(
                asset?.Class ?? AssetClass.Crypto,
                position,
                feesByAsset.GetValueOrDefault(group.Key, Money.Euros(0m)),
                realizedByAsset.GetValueOrDefault(group.Key, Money.Euros(0m))));

            responsesByAsset[group.Key] = (symbol, asset?.IsVerified ?? false);
        }

        var valued = PortfolioCalculationService.Value(transactions, transfers);
        var balances = CashBalanceCalculator.Calculate(valued);
        var cashTotal = CashBalanceCalculator.InEuros(balances, await RatesAsync(balances, cancellationToken).ConfigureAwait(false));

        var summary = PortfolioSummary.Build(
            portfolioAssets,
            cashTotal,
            balances,
            IncomeByClass(incomes, assets),

            // Todo lo realizado, también lo de activos vendidos por completo: su pérdida
            // o su ganancia no puede desaparecer del acumulado por dejar de tenerlos.
            realized.Aggregate(Money.Euros(0m), (total, result) => total + result.ResultInEuros),
            ContributedCapitalCalculator.Calculate(valued));

        // Los niveles vienen de la última señal de entrada de cada activo: el sistema que
        // propuso entrar es el que dijo dónde salir, y repetirlos aquí sería inventarlos.
        var levels = await LevelsAsync(cancellationToken).ConfigureAwait(false);

        var aliases = await context.Accounts
            .ToDictionaryAsync(account => account.Id, account => account.Alias, cancellationToken)
            .ConfigureAwait(false);

        return new PortfolioResponse(
            [.. summary.Groups.Select(group => ToResponse(group, responsesByAsset, levels))],
            [.. balances.Balances.Select(balance => new CashBalanceResponse(
                balance.AccountId,
                aliases.GetValueOrDefault(balance.AccountId, string.Empty),
                balance.Currency.Code,
                balance.Amount.Amount))],
            cashTotal.Total.Amount,
            [.. cashTotal.CurrenciesWithoutRate.Select(currency => currency.Code)],
            summary.CostInEuros.Amount,
            summary.MarketValueInEuros.Amount,
            summary.Wealth.TotalInEuros.Amount,
            summary.Result.RealizedInEuros.Amount,
            summary.Result.UnrealisedInEuros.Amount,
            [.. summary.Income.Select(entry => new IncomeByClassResponse(
                entry.Class.ToString(),
                entry.GrossInEuros.Amount,
                entry.WithholdingInEuros.Amount,
                entry.NetInEuros.Amount))],
            transactions.Count(transaction => transaction.RequiresReview),
            transfers.Count(transfer => transfer.Status == InternalTransferStatus.Proposed),
            [.. inconsistencies
                .OrderByDescending(inconsistency => inconsistency.OccurredAt.Instant)
                .Select(inconsistency => new InconsistencyResponse(
                    inconsistency.Kind.ToString(),
                    inconsistency.AssetId,
                    assets.GetValueOrDefault(inconsistency.AssetId)?.CanonicalSymbol ?? string.Empty,
                    inconsistency.TransactionId,
                    inconsistency.OccurredAt.Instant,
                    inconsistency.MissingQuantity.Value))],
            summary.Wealth.MissingPrices,
            summary.Wealth.MissingCash,
            Risk(summary, responsesByAsset),
            Weights(summary, responsesByAsset),
            Contributed(summary));
    }

    /// <summary>Lo puesto frente a lo que hay, en la forma que lee el cliente.</summary>
    private static ContributedCapitalResponse Contributed(PortfolioSummary summary)
    {
        var wealth = summary.Wealth.TotalInEuros;
        var contributed = summary.Contributed;

        return new ContributedCapitalResponse(
            contributed.DepositedInEuros.Amount,
            contributed.WithdrawnInEuros.Amount,
            contributed.NetInEuros.Amount,
            contributed.ResultAgainst(wealth).Amount,
            contributed.ShareAgainst(wealth),
            contributed.MissesAssetsFromOutside);
    }

    /// <summary>
    /// Cuánto concentra la cartera, si se pasa del umbral.
    /// </summary>
    /// <remarks>
    /// Tres activos y un setenta por ciento: no dice que concentrar esté mal, dice cuánto
    /// se está concentrando, que es lo que no se ve mirando una tabla de doce filas.
    /// </remarks>
    private static ConcentrationResponse? Risk(
        PortfolioSummary summary,
        IReadOnlyDictionary<Guid, (string Symbol, bool Verified)> named)
    {
        var warning = Domain.Risk.Concentration.Check(
            [.. Weighted(summary, named)], TopPositions, ConcentrationThreshold);

        return warning is null
            ? null
            : new ConcentrationResponse(
                [.. warning.Top.Select(weight => Weight(weight))],
                warning.Share,
                warning.Threshold);
    }

    private static (Money? Target, Money? Stop) Level(
        IReadOnlyDictionary<Guid, (Money? Target, Money? Stop)> levels,
        Guid assetId) =>
        levels.TryGetValue(assetId, out var level) ? level : (null, null);

    private static bool Reached(OpenPosition position, Money? level, bool above) =>
        position.MarketPriceInEuros is { } price
        && level is { } target
        && (above ? price.Amount >= target.Amount : price.Amount <= target.Amount);

    private static IReadOnlyList<RiskWeightResponse> Weights(
        PortfolioSummary summary,
        IReadOnlyDictionary<Guid, (string Symbol, bool Verified)> named) =>
        [.. Weighted(summary, named).OrderByDescending(weight => weight.Share).Select(weight => Weight(weight))];

    private static RiskWeightResponse Weight(Domain.Risk.Weight weight) =>
        new(weight.Name, weight.ValueInEuros.Amount, weight.Share, weight.Share > PositionCap);

    private static IEnumerable<Domain.Risk.Weight> Weighted(
        PortfolioSummary summary,
        IReadOnlyDictionary<Guid, (string Symbol, bool Verified)> named) =>
        summary.Positions
            .Where(position => position.Weight is not null)
            .Select(position => new Domain.Risk.Weight(
                named.GetValueOrDefault(
                    position.Position.AssetId,
                    (Symbol: position.Position.AssetId.ToString(), Verified: false)).Symbol,
                position.Position.MarketValueInEuros ?? Money.Euros(0m),
                position.Weight!.Value));

    /// <summary>
    /// El objetivo y el nivel de salida vigentes de cada activo.
    /// </summary>
    /// <remarks>
    /// De la última señal de entrada, que es la que los fijó. Sin señales no hay niveles,
    /// y entonces la posición se enseña sin ellos en lugar de con unos calculados.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Guid, (Money? Target, Money? Stop)>> LevelsAsync(
        CancellationToken cancellationToken)
    {
        var signals = await context.EmittedSignals
            .Where(signal => signal.Direction == Domain.Strategies.SignalDirection.Entry)
            .OrderByDescending(signal => signal.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return signals
            .GroupBy(signal => signal.AssetId)
            .ToDictionary(
                group => group.Key,
                group => (group.First().Target, Stop: group.First().StopLoss));
    }

    private static PortfolioGroupResponse ToResponse(
        PortfolioGroup group,
        IReadOnlyDictionary<Guid, (string Symbol, bool Verified)> named,
        IReadOnlyDictionary<Guid, (Money? Target, Money? Stop)> levels) =>
        new(
            group.Class.ToString(),
            [.. group.Positions.Select(entry =>
            {
                var position = entry.Position;
                var asset = named.GetValueOrDefault(position.AssetId, (Symbol: position.AssetId.ToString(), Verified: false));

                return new OpenPositionResponse(
                    position.AssetId,
                    asset.Symbol,
                    asset.Verified,
                    position.Quantity.Value,
                    position.CostInEuros.Amount,
                    position.AverageCostInEuros.Amount,
                    position.MarketPriceInEuros?.Amount,
                    position.MarketValueInEuros?.Amount,
                    position.UnrealisedResultInEuros?.Amount,
                    position.PriceAsOf,
                    entry.Class.ToString(),
                    entry.FeesInEuros.Amount,
                    entry.RealizedResultInEuros.Amount,
                    entry.Weight,
                    Level(levels, position.AssetId).Target?.Amount,
                    Level(levels, position.AssetId).Stop?.Amount,

                    // Alcanzado significa que el último precio ya está en el nivel. Es un
                    // aviso, no una orden: Kapea no ejecuta nada.
                    Reached(position, Level(levels, position.AssetId).Target, above: true),
                    Reached(position, Level(levels, position.AssetId).Stop, above: false));
            })],
            group.CostInEuros.Amount,
            group.MarketValueInEuros.Amount,
            group.Weight);

    private static IReadOnlyList<IncomeByClass> IncomeByClass(
        IReadOnlyList<CapitalIncome> incomes,
        IReadOnlyDictionary<Guid, Asset> assets) =>
        [.. incomes
            .Where(income => income.AssetId is not null && assets.ContainsKey(income.AssetId.Value))
            .GroupBy(income => assets[income.AssetId!.Value].Class)
            .Select(group => new IncomeByClass(
                group.Key,
                group.Aggregate(Money.Euros(0m), (total, income) => total + income.GrossAmountInEuros),
                group.Aggregate(Money.Euros(0m), (total, income) => total + income.WithholdingInEuros)))];

    /// <summary>
    /// Tipos del día para las divisas que no son el euro.
    /// </summary>
    /// <remarks>
    /// La que no tenga tipo se queda fuera del total y se nombra: contarla como cero
    /// escondería dinero y convertirla a ojo inventaría una cifra.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Currency, ExchangeRate>> RatesAsync(
        CashBalances balances,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rates = new Dictionary<Currency, ExchangeRate>();

        foreach (var currency in balances.Balances
            .Select(balance => balance.Currency)
            .Where(currency => !currency.IsEuro)
            .Distinct())
        {
            if (await exchangeRates.ResolveAsync(currency, today, cancellationToken).ConfigureAwait(false) is { } rate)
            {
                rates[currency] = rate;
            }
        }

        return rates;
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
        var origins = await OriginsAsync(ofYear, cancellationToken).ConfigureAwait(false);

        var details = ofYear
            .OrderBy(result => result.DisposedAt.Instant)
            .Select(result => ToResponse(result, symbols, origins))
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
            : ToResponse(
                result,
                await SymbolsAsync(cancellationToken).ConfigureAwait(false),
                await OriginsAsync([result], cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Procedencia de las ventas y de las compras que consumen, para el detalle fiscal.
    /// </summary>
    /// <remarks>
    /// Es lo primero que se pregunta al revisar un resultado: qué parte se apoya en datos
    /// apuntados a mano y qué parte en lo que trajo la plataforma.
    /// </remarks>
    private async Task<Dictionary<Guid, string>> OriginsAsync(
        IReadOnlyCollection<RealizedResult> results,
        CancellationToken cancellationToken)
    {
        var ids = results
            .Select(result => result.DisposalTransactionId)
            .Concat(results.SelectMany(result => result.ConsumedLots).Select(lot => lot.AcquisitionTransactionId))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var fileNames = await ImportFileNamesAsync(cancellationToken).ConfigureAwait(false);
        var transactions = await context.Transactions
            .IgnoreQueryFilters(TransactionQueryFilters.OnlyInForce)
            .Where(transaction => ids.Contains(transaction.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return transactions.ToDictionary(transaction => transaction.Id, transaction => OriginOf(transaction, fileNames));
    }

    public async Task<PendingReviewResponse> CountPendingReviewAsync(CancellationToken cancellationToken = default)
    {
        // Dos recuentos en la base y no una lista: los filtros globales ya dejan sólo
        // lo del usuario, y el número es lo único que se necesita.
        var transfers = await context.InternalTransfers
            .CountAsync(transfer => transfer.Status == InternalTransferStatus.Proposed, cancellationToken)
            .ConfigureAwait(false);

        var transactions = await context.Transactions
            .CountAsync(transaction => transaction.Type == TransactionType.Unknown, cancellationToken)
            .ConfigureAwait(false);

        var duplicates = (await ManualDuplicatePairsAsync(cancellationToken).ConfigureAwait(false)).Count;

        return new PendingReviewResponse(transfers, transactions, duplicates);
    }

    public async Task<IReadOnlyList<ManualDuplicateResponse>> ListManualDuplicatesAsync(CancellationToken cancellationToken = default)
    {
        var pairs = await ManualDuplicatePairsAsync(cancellationToken).ConfigureAwait(false);

        if (pairs.Count == 0)
        {
            return [];
        }

        var symbols = await SymbolsAsync(cancellationToken).ConfigureAwait(false);
        var profileNames = await context.ImportProfiles
            .ToDictionaryAsync(profile => profile.Id, profile => profile.Name, cancellationToken)
            .ConfigureAwait(false);
        var fileNames = await ImportFileNamesAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. pairs
                .OrderByDescending(pair => pair.Manual.OccurredAt.Instant)
                .Select(pair => new ManualDuplicateResponse(
                    ToResponse(pair.Manual, symbols, profileNames, fileNames),
                    ToResponse(pair.Imported, symbols, profileNames, fileNames))),
        ];
    }

    /// <summary>
    /// Apuntes manuales vigentes y los importados que coinciden con ellos.
    /// </summary>
    /// <remarks>
    /// Los manuales son pocos, así que se cargan todos y sólo se piden los importados que
    /// podrían coincidir: mismas cuentas, activos y tipos, en las fechas de los manuales
    /// con un día de margen por la zona horaria. La comparación fina la hace el dominio.
    /// </remarks>
    private async Task<List<(Transaction Manual, Transaction Imported)>> ManualDuplicatePairsAsync(CancellationToken cancellationToken)
    {
        var manuals = await context.Transactions
            .Where(transaction => transaction.Origin == TransactionOrigin.Manual)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (manuals.Count == 0)
        {
            return [];
        }

        var accounts = manuals.Select(manual => manual.AccountId).Distinct().ToList();
        var assets = manuals.Where(manual => manual.AssetId != null).Select(manual => manual.AssetId!.Value).Distinct().ToList();
        var from = manuals.Min(manual => manual.OccurredAt.Instant).AddDays(-1);
        var to = manuals.Max(manual => manual.OccurredAt.Instant).AddDays(1);

        var candidates = await context.Transactions
            .Where(transaction => transaction.Origin == TransactionOrigin.Imported
                && accounts.Contains(transaction.AccountId)
                && (transaction.AssetId == null || assets.Contains(transaction.AssetId.Value))
                && transaction.OccurredAt.Instant >= from
                && transaction.OccurredAt.Instant <= to)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. from manual in manuals
               from imported in candidates
               where ManualCoincidence.Matches(manual, imported)
               select (manual, imported),
        ];
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

    private static RealizedResultResponse ToResponse(
        RealizedResult result,
        Dictionary<Guid, string> symbols,
        IReadOnlyDictionary<Guid, string> origins) =>
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
                        lot.ResultInEuros.Amount,
                        origins.GetValueOrDefault(lot.AcquisitionTransactionId))),
            ],
            origins.GetValueOrDefault(result.DisposalTransactionId));

    /// <summary>Cuántas filas se enseñan interpretadas. Suficientes para ver un mapeo mal puesto.</summary>
    private const int SampleSize = 10;

    /// <summary>Con qué perfil se leyó, para poder decirlo en la pantalla de la importación.</summary>
    private async Task<string?> ProfileNameAsync(ImportRun run, CancellationToken cancellationToken) =>
        run.ProfileId is { } profileId
            ? await context.ImportProfiles
                .Where(profile => profile.Id == profileId)
                .Select(profile => profile.Name)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

    private static ImportRunResponse ToResponse(ImportRun run, string? profileName = null) =>
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
            ],
            profileName,
            run.ProfileVersion)
        {
            Sample =
            [
                .. run.Records
                    .Where(record => record.Outcome != StagedRecordOutcome.Rejected)
                    .OrderBy(record => record.RowNumber ?? int.MaxValue)
                    .Take(SampleSize)
                    .Select(Interpreted),
            ],
        };

    private static InterpretedRowResponse Interpreted(StagedRecord staged)
    {
        var record = StagedRecordReader.Read(staged.Payload, staged.RawContent);

        return new InterpretedRowResponse(
            staged.RowNumber,
            record.ToOccurrence().Instant,
            record.Type.ToString(),
            record.AssetSymbol,
            record.Quantity,
            record.GrossAmount,
            record.Currency.Code,
            record.Fee,
            staged.Outcome.ToString(),
            record.Sheet);
    }

    /// <summary>
    /// Evolución de la cartera, reconstruida de los movimientos y la serie de precios.
    /// </summary>
    /// <remarks>
    /// Se calcula en cada consulta y se guarda un rato en memoria. Recorrer un par de
    /// miles de movimientos son milisegundos, y a cambio no hay ninguna cifra guardada
    /// que pueda quedarse vieja cuando entre una importación con fecha anterior.
    /// </remarks>
    public async Task<PortfolioHistoryResponse> GetHistoryAsync(
        DateOnly? from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Sin fecha de inicio se enseña desde el primer movimiento. Cuánto hay depende de
        // la cartera, así que lo resuelve quien la conoce y no quien pregunta.
        var desde = from ?? await FirstMovementAsync(to, cancellationToken).ConfigureAwait(false);

        var days = await DaysAsync(desde, to, cancellationToken).ConfigureAwait(false);

        var classes = await context.Assets
            .ToDictionaryAsync(asset => asset.Id, asset => asset.Class, cancellationToken)
            .ConfigureAwait(false);

        return new PortfolioHistoryResponse(
            [
                .. days.Select(day => new PortfolioHistoryDayResponse(
                    day.Date,
                    day.ValueInEuros.Amount,
                    day.NetContributionInEuros.Amount,
                    day.IsComplete,
                    day.HasCarriedPrices)),
            ],
            [
                .. PortfolioHistory.ByClass(days, classes).Select(day => new ClassHistoryDayResponse(
                    day.Date,
                    day.ValueByClass.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value.Amount))),
            ],
            days.Count(day => !day.IsComplete));
    }

    public async Task<AssetHistoryResponse?> GetAssetHistoryAsync(
        Guid assetId,
        DateOnly? from,
        DateOnly to,
        int indicatorWindowDays,
        CancellationToken cancellationToken = default)
    {
        var asset = await context.Assets
            .FirstOrDefaultAsync(entity => entity.Id == assetId, cancellationToken)
            .ConfigureAwait(false);

        if (asset is null)
        {
            return null;
        }

        // Sin fecha de inicio se enseña todo lo que hay. Se resuelve aquí y no en quien
        // pregunta porque depende del activo: uno tiene veinte años y otro tres meses.
        var desde = from ?? (await priceHistory
            .GetStoredRangeAsync([assetId], cancellationToken)
            .ConfigureAwait(false))
            .GetValueOrDefault(assetId)?.First ?? to;

        // La cotización se pide aparte de lo que alimenta la reconstrucción de la
        // cartera. Ampliar aquella lista haría que consultar una gráfica cambiara el
        // valor del patrimonio, que es lo último que nadie espera de mirar un gráfico.
        var quotes = await priceHistory
            .GetAsync(assetId, desde, to, cancellationToken)
            .ConfigureAwait(false);

        var days = PortfolioHistory.ForAsset(
            await DaysAsync(desde, to, cancellationToken).ConfigureAwait(false),
            assetId,
            quotes.ToDictionary(price => price.Date, price => Money.Euros(price.PriceInEuros)));

        // Los indicadores se calculan solo sobre los días con precio: rellenar los huecos
        // daría una media de lo que dice el relleno, no de lo que hizo el mercado.
        var series = days
            .Where(day => day.PriceInEuros is not null)
            .Select(day => new PricePoint(day.Date, day.PriceInEuros!.Value.Amount))
            .ToList();

        return new AssetHistoryResponse(
            assetId,
            asset.CanonicalSymbol,
            [
                .. days.Select(day => new AssetHistoryDayResponse(
                    day.Date, day.Quantity.Value, day.PriceInEuros?.Amount, day.ValueInEuros?.Amount, day.CarriedFrom)),
            ],
            Points(TechnicalIndicators.SimpleMovingAverage(series, indicatorWindowDays)),
            Points(TechnicalIndicators.ExponentialMovingAverage(series, indicatorWindowDays)),
            Points(TechnicalIndicators.RelativeStrengthIndex(series, indicatorWindowDays)),
            indicatorWindowDays);
    }

    private static IReadOnlyList<IndicatorPointResponse> Points(IReadOnlyList<IndicatorPoint> points) =>
        [.. points.Select(point => new IndicatorPointResponse(point.Date, point.Value))];

    /// <summary>
    /// El día del primer movimiento, o el final del rango si no hay ninguno.
    /// </summary>
    /// <remarks>
    /// Sin movimientos no hay nada que enseñar, y devolver un solo día vacío lo dice sin
    /// necesidad de un caso aparte en quien pregunta.
    /// </remarks>
    private async Task<DateOnly> FirstMovementAsync(DateOnly to, CancellationToken cancellationToken)
    {
        var first = await context.Transactions
            .OrderBy(transaction => transaction.OccurredAt.Instant)
            .Select(transaction => (DateTimeOffset?)transaction.OccurredAt.Instant)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return first is { } instant ? DateOnly.FromDateTime(instant.UtcDateTime) : to;
    }

    private async Task<IReadOnlyList<PortfolioDay>> DaysAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var transactions = await context.Transactions.ToListAsync(cancellationToken).ConfigureAwait(false);
        var transfers = await context.InternalTransfers.ToListAsync(cancellationToken).ConfigureAwait(false);

        var assetIds = transactions
            .Where(transaction => transaction.AssetId is not null)
            .Select(transaction => transaction.AssetId!.Value)
            .Distinct()
            .ToList();

        var prices = await priceHistory.GetAsync(assetIds, from, to, cancellationToken).ConfigureAwait(false);

        // La clase decide de qué mercado es cada activo, que es lo que permite deducir
        // qué días no cotizó. El cierre anterior al rango permite arrastrarlo el primer
        // día si cae en mercado cerrado.
        var classes = await context.Assets
            .Where(asset => assetIds.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.Class })
            .ToDictionaryAsync(asset => asset.Id, asset => asset.Class, cancellationToken)
            .ConfigureAwait(false);

        var priorCloses = await priceHistory
            .GetLastBeforeAsync(assetIds, from, cancellationToken)
            .ConfigureAwait(false);

        return PortfolioHistory.Build(
            PortfolioCalculationService.Value(transactions, transfers),
            prices,
            from,
            to,
            classes,
            priorCloses);
    }

    /// <summary>
    /// Rendimiento del periodo y comparación con la referencia.
    /// </summary>
    /// <remarks>
    /// Sin referencia elegida se toma la mayor posición del último día. Es la comparación
    /// más honesta que se puede hacer sin preguntar: qué habría pasado poniendo todo el
    /// dinero, en las mismas fechas, en lo que ya es su mayor apuesta.
    /// </remarks>
    public async Task<PerformanceResponse> GetPerformanceAsync(
        DateOnly from,
        DateOnly to,
        Guid? benchmarkAssetId = null,
        CancellationToken cancellationToken = default)
    {
        var days = await DaysAsync(from, to, cancellationToken).ConfigureAwait(false);
        var performance = PortfolioPerformance.Of(days);

        var reference = benchmarkAssetId ?? Largest(days);
        var contributed = days.Aggregate(Money.Euros(0m), (total, day) => total + day.NetContributionInEuros);

        if (reference is not { } assetId)
        {
            return Response(from, to, performance, contributed, days, null, null);
        }

        var asset = await context.Assets
            .FirstOrDefaultAsync(entity => entity.Id == assetId, cancellationToken)
            .ConfigureAwait(false);

        var prices = await priceHistory.GetAsync(assetId, from, to, cancellationToken).ConfigureAwait(false);
        var benchmark = BenchmarkComparison.Of(days, prices);

        return Response(from, to, performance, contributed, days, asset?.CanonicalSymbol, benchmark);
    }

    /// <summary>
    /// La mayor posición del último día con precios, que es la referencia por omisión.
    /// </summary>
    /// <remarks>
    /// El último día del periodo suele ser hoy, y hoy todavía no ha cerrado: mirar solo
    /// ese día dejaba la comparación sin referencia toda la jornada.
    /// </remarks>
    private static Guid? Largest(IReadOnlyList<PortfolioDay> days) =>
        days
            .Reverse()
            .Select(day => day.Assets
                .Where(asset => asset.ValueInEuros is not null)
                .OrderByDescending(asset => asset.ValueInEuros!.Value.Amount)
                .Select(asset => (Guid?)asset.AssetId)
                .FirstOrDefault())
            .FirstOrDefault(assetId => assetId is not null);

    /// <summary>Valor del último día que lo tiene, o cero si ninguno.</summary>
    private static Money LastValued(IReadOnlyList<PortfolioDay> days) =>
        days.LastOrDefault(day => day.IsComplete)?.ValueInEuros ?? Money.Euros(0m);

    private static PerformanceResponse Response(
        DateOnly from,
        DateOnly to,
        PerformanceResult performance,
        Money contributed,
        IReadOnlyList<PortfolioDay> days,
        string? benchmarkSymbol,
        BenchmarkResult? benchmark)
    {
        // El último día con valor, no el último del periodo: hoy todavía no ha cerrado y
        // enseñar su cero haría parecer que la cartera vale nada.
        var value = LastValued(days);

        // El rendimiento de la referencia se mide con la misma regla que el de la
        // cartera, o no serían comparables.
        var benchmarkReturn = benchmark is null ? null : (decimal?)PortfolioPerformance.Of(benchmark.Days).TimeWeighted;

        return new PerformanceResponse(
            from,
            to,
            performance.TimeWeighted,
            performance.MoneyWeighted,
            performance.Volatility,
            performance.MaximumDrawdown,
            performance.DrawdownRecoveredInDays,
            contributed.Amount,
            value.Amount,
            benchmarkSymbol,
            benchmarkReturn,
            benchmark is null ? null : LastValued(benchmark.Days).Amount,
            performance.IsComplete,
            benchmark?.IsComplete ?? false);
    }
}
