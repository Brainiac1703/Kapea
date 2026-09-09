using Kapea.Application.Abstractions;
using Kapea.Application.Import;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.Import;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class ImportPipelineTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Staging_classifies_the_records_without_writing_anything_to_the_domain()
    {
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, Read([Buy("TX-1"), Buy("TX-2")]));

        Assert.Equal(ImportRunStatus.Staged, run.Status);
        Assert.Equal(2, run.RecordsImported);

        await using var context = fixture.CreateContext(scenario.Owner);

        Assert.Empty(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task Abandoning_the_preview_leaves_no_transactions_and_no_completed_run()
    {
        var scenario = await NewScenarioAsync();
        var run = await StageAsync(scenario, Read([Buy("TX-1")]));

        await WithPipelineAsync(scenario, pipeline => pipeline.DiscardAsync(run.Id));

        await using var context = fixture.CreateContext(scenario.Owner);

        Assert.Empty(await context.Transactions.ToListAsync());
        Assert.Equal(
            ImportRunStatus.Discarded,
            (await context.ImportRuns.SingleAsync(stored => stored.Id == run.Id)).Status);
    }

    [Fact]
    public async Task Confirming_materialises_the_transactions_with_their_source_record()
    {
        var scenario = await NewScenarioAsync();
        var run = await StageAsync(scenario, Read([Buy("TX-1")]));

        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id));

        await using var context = fixture.CreateContext(scenario.Owner);
        var transaction = await context.Transactions.SingleAsync();

        Assert.Equal(TransactionType.Buy, transaction.Type);
        Assert.Equal(Money.Euros(1000m), transaction.GrossAmount);
        Assert.Equal(run.Id, transaction.Source.ImportRunId);
        Assert.Equal("TX-1", transaction.Source.NaturalId);
        Assert.Equal("raw:TX-1", transaction.Source.RawContent);
        Assert.Equal(
            ImportRunStatus.Completed,
            (await context.ImportRuns.SingleAsync(stored => stored.Id == run.Id)).Status);
    }

    [Fact]
    public async Task Reimporting_the_same_records_discards_them_all_as_duplicates()
    {
        var scenario = await NewScenarioAsync();
        var records = Read([Buy("TX-1"), Buy("TX-2")]);

        var first = await StageAsync(scenario, records);
        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(first.Id));

        var second = await StageAsync(scenario, records);

        Assert.Equal(0, second.RecordsImported);
        Assert.Equal(2, second.DuplicatesDiscarded);
    }

    [Fact]
    public async Task Overlapping_ranges_only_bring_in_what_was_missing()
    {
        var scenario = await NewScenarioAsync();

        var first = await StageAsync(scenario, Read([Buy("TX-1"), Buy("TX-2")]));
        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(first.Id));

        var second = await StageAsync(scenario, Read([Buy("TX-2"), Buy("TX-3")]));

        Assert.Equal(1, second.RecordsImported);
        Assert.Equal(1, second.DuplicatesDiscarded);
    }

    [Fact]
    public async Task Two_identical_records_with_different_ids_are_both_imported()
    {
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, Read([Buy("TX-1"), Buy("TX-2")]));

        Assert.Equal(2, run.RecordsImported);
    }

    [Fact]
    public async Task The_same_record_twice_in_one_batch_is_only_imported_once()
    {
        // La comprobación contra la base de datos no ve el duplicado dentro del propio
        // lote, porque todavía no está escrito: hace falta el control en memoria.
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, Read([Buy("TX-1"), Buy("TX-1")]));

        Assert.Equal(1, run.RecordsImported);
        Assert.Equal(1, run.DuplicatesDiscarded);
    }

    [Fact]
    public async Task Rejected_records_keep_their_content_and_reason_without_stopping_the_rest()
    {
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, new ImportReadResult(
            [Buy("TX-1")],
            [new RejectedRecord(null, 7, "fila;ilegible", "La fecha no se puede interpretar.")],
            NonFinancialRecordCount: 2));

        var rejected = Assert.Single(run.Records, record => record.Outcome == StagedRecordOutcome.Rejected);

        Assert.Equal(1, run.RecordsImported);
        Assert.Equal(2, run.NonFinancialRecords);
        Assert.Equal("fila;ilegible", rejected.RawContent);
        Assert.Contains("fecha", rejected.RejectionReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reprocessing_imports_what_can_now_be_read_and_keeps_the_rest_rejected()
    {
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, new ImportReadResult(
            [],
            [
                new RejectedRecord(null, 7, "recuperable", "Ilegible."),
                new RejectedRecord(null, 8, "irrecuperable", "Ilegible."),
            ]));

        await WithPipelineAsync(scenario, pipeline => pipeline.ReprocessRejectedAsync(
            run.Id,
            staged => staged.RawContent == "recuperable" ? Buy("TX-R", raw: "recuperable") : null));

        await using var context = fixture.CreateContext(scenario.Owner);
        var reloaded = await context.ImportRuns.Include(stored => stored.Records)
            .SingleAsync(stored => stored.Id == run.Id);

        Assert.Equal(1, reloaded.RecordsImported);
        Assert.Equal(1, reloaded.RecordsRejected);
    }

    [Fact]
    public async Task A_record_that_could_never_become_a_movement_is_rejected_in_the_preview()
    {
        // La vista previa tiene que decir la verdad: si un registro no puede llegar a ser
        // un movimiento, se rechaza aquí y no al confirmar, cuando ya se ha prometido.
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, Read([Buy("TX-1") with { AssetSymbol = null }]));

        Assert.Equal(0, run.RecordsImported);
        Assert.Equal(1, run.RecordsRejected);
        Assert.Contains(
            "necesita activo",
            Assert.Single(run.Records, record => record.Outcome == StagedRecordOutcome.Rejected).RejectionReason!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirming_a_preview_never_fails_over_a_record_it_promised()
    {
        var scenario = await NewScenarioAsync();

        var run = await StageAsync(scenario, Read([Buy("TX-1"), Buy("TX-2") with { AssetSymbol = null }]));

        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id));

        await using var context = fixture.CreateContext(scenario.Owner);

        Assert.Equal(1, run.RecordsImported);
        Assert.Single(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task A_failure_halfway_leaves_no_transactions_and_a_failed_run()
    {
        var scenario = await NewScenarioAsync();

        // Una operación en dólares sin tipo de cambio ingestado: falla al materializar,
        // después de que el primer registro ya se haya construido.
        var run = await StageAsync(scenario, Read([Buy("TX-1"), Buy("TX-2", currency: "USD")]));

        await Assert.ThrowsAsync<DomainException>(
            () => WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id)));

        await using var context = fixture.CreateContext(scenario.Owner);
        var reloaded = await context.ImportRuns.SingleAsync(stored => stored.Id == run.Id);

        Assert.Empty(await context.Transactions.ToListAsync());
        Assert.Equal(ImportRunStatus.Failed, reloaded.Status);
        Assert.Contains("tipo de cambio", reloaded.FailureReason!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transaction_traces_back_to_its_run_and_its_original_record()
    {
        var scenario = await NewScenarioAsync();
        var run = await StageAsync(scenario, Read([Buy("TX-1")]));
        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id));

        await using var context = fixture.CreateContext(scenario.Owner);
        var transaction = await context.Transactions.SingleAsync();
        var origin = await context.ImportRuns.SingleAsync(stored => stored.Id == transaction.Source.ImportRunId);

        Assert.Equal(Platform.Kraken, origin.Platform);
        Assert.Equal("raw:TX-1", transaction.Source.RawContent);
    }

    [Fact]
    public async Task Deleting_a_run_removes_it_with_its_transactions()
    {
        var scenario = await NewScenarioAsync();
        var run = await StageAsync(scenario, Read([Buy("TX-1")]));
        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id));

        await WithPipelineAsync(scenario, pipeline => pipeline.DeleteAsync(run.Id, _ => []));

        await using var context = fixture.CreateContext(scenario.Owner);

        Assert.Empty(await context.Transactions.ToListAsync());
        Assert.Empty(await context.ImportRuns.Where(stored => stored.Id == run.Id).ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_run_with_dependencies_is_blocked_and_lists_them()
    {
        var scenario = await NewScenarioAsync();
        var run = await StageAsync(scenario, Read([Buy("TX-1")]));
        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id));

        var exception = await Assert.ThrowsAsync<DomainException>(() => WithPipelineAsync(
            scenario,
            pipeline => pipeline.DeleteAsync(run.Id, _ => ["participa en un traspaso confirmado"])));

        Assert.Contains("traspaso confirmado", exception.Message, StringComparison.Ordinal);

        await using var context = fixture.CreateContext(scenario.Owner);

        Assert.Single(await context.Transactions.ToListAsync());
    }

    [Fact]
    public async Task An_unknown_symbol_becomes_an_unverified_asset_instead_of_stopping_the_import()
    {
        var scenario = await NewScenarioAsync();
        var run = await StageAsync(scenario, Read([Buy("TX-1", symbol: "RAREZA")]));

        await WithPipelineAsync(scenario, pipeline => pipeline.ConfirmAsync(run.Id));

        await using var context = fixture.CreateContext(scenario.Owner);
        var asset = await context.Assets.SingleAsync(stored => stored.CanonicalSymbol == "RAREZA");

        Assert.False(asset.IsVerified);
        Assert.Equal(asset.Id, (await context.Transactions.SingleAsync()).AssetId);
    }

    [Fact]
    public async Task Importing_into_an_account_that_does_not_exist_is_rejected()
    {
        var scenario = await NewScenarioAsync();

        await Assert.ThrowsAsync<ImportTargetException>(() => WithPipelineAsync(
            scenario,
            pipeline => pipeline.StageAsync(Guid.NewGuid(), Read([Buy("TX-1")]))));
    }

    private static ImportReadResult Read(IReadOnlyList<ImportRecord> records) => new(records, []);

    private static ImportRecord Buy(
        string naturalId,
        string symbol = "BTC",
        string currency = "EUR",
        string? raw = null) =>
        new(
            naturalId, null, TransactionType.Buy, symbol, AssetClass.Crypto, 0.1m, 10000m, 1000m,
            Currency.FromCode(currency), 1m, null,
            new DateTimeOffset(2024, 1, 10, 10, 0, 0, TimeSpan.Zero), null, "UTC", null,
            raw ?? $"raw:{naturalId}");

    private async Task<ImportRun> StageAsync(Scenario scenario, ImportReadResult read)
    {
        ImportRun? run = null;

        await WithPipelineAsync(scenario, async pipeline =>
            run = await pipeline.StageAsync(scenario.AccountId, read, "export.csv"));

        return run!;
    }

    private async Task WithPipelineAsync(Scenario scenario, Func<ImportPipeline, Task> work)
    {
        await using var context = fixture.CreateContext(scenario.Owner);

        await work(new ImportPipeline(
            new ImportRepository(context),
            new AssetCatalog(context, NullLogger<AssetCatalog>.Instance),
            new NoRatesProvider(),
            new FixedUser(scenario.Owner),
            new FakeTimeProvider(Now),
            NullLogger<ImportPipeline>.Instance));
    }

    private async Task<Scenario> NewScenarioAsync()
    {
        var owner = new UserId(Guid.NewGuid());
        var account = PlatformAccount.Create(owner, Platform.Kraken, "Kraken", Currency.Euro);

        await using var context = fixture.CreateContext(owner);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        return new Scenario(owner, account.Id);
    }

    /// <summary>Sin tipos ingestados: cualquier operación en divisa falla, que es lo que se quiere provocar.</summary>
    private sealed class NoRatesProvider : IExchangeRateProvider
    {
        public Task<ExchangeRate?> ResolveAsync(
            Currency currency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExchangeRate?>(null);
    }

    private sealed class FixedUser(UserId id) : ICurrentUser
    {
        public UserId Id => id;
    }

    private sealed record Scenario(UserId Owner, Guid AccountId);
}
