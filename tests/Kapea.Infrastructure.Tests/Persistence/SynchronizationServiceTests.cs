using Kapea.Application.Abstractions;
using Kapea.Application.Credentials;
using Kapea.Application.Import;
using Kapea.Application.Synchronization;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Credentials;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Kapea.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class SynchronizationServiceTests(SqlServerFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_first_run_asks_for_the_whole_history_and_imports_it()
    {
        var world = await NewWorldAsync();
        var adapter = new RecordingAdapter(Platform.Kraken, [Buy("TX-1")]);

        var report = await RunAsync(world, adapter);

        var result = Assert.Single(report.Results);

        Assert.Equal(SynchronizationOutcome.Imported, result.Outcome);
        Assert.Equal(1, result.ImportedRecords);
        Assert.Equal(SynchronizationService.EarliestHistory, Assert.Single(adapter.RequestedRanges).From);
    }

    [Fact]
    public async Task A_second_run_only_asks_for_what_comes_after_the_last_successful_import()
    {
        var world = await NewWorldAsync();

        await RunAsync(world, new RecordingAdapter(Platform.Kraken, [Buy("TX-1")]));

        var second = new RecordingAdapter(Platform.Kraken, [Buy("TX-2")]);
        await RunAsync(world, second);

        Assert.Equal(Now, Assert.Single(second.RequestedRanges).From);
    }

    [Fact]
    public async Task A_failed_run_does_not_move_the_point_the_next_one_starts_from()
    {
        var world = await NewWorldAsync();
        await RunAsync(world, new FailingAdapter(Platform.Kraken, new InvalidOperationException("la red falló")));

        var next = new RecordingAdapter(Platform.Kraken, []);
        await RunAsync(world, next);

        Assert.Equal(SynchronizationService.EarliestHistory, Assert.Single(next.RequestedRanges).From);
    }

    [Fact]
    public async Task A_second_concurrent_run_of_the_same_account_is_skipped()
    {
        var world = await NewWorldAsync();

        await using var context = fixture.CreateContext(world.Owner);
        var holder = new AccountSyncLock(context, new FakeTimeProvider(Now), NullLogger<AccountSyncLock>.Instance);

        await using var held = await holder.TryAcquireAsync(world.AccountId, TimeSpan.FromHours(1));
        Assert.NotNull(held);

        var adapter = new RecordingAdapter(Platform.Kraken, [Buy("TX-1")]);
        var report = await RunAsync(world, adapter);

        Assert.Equal(SynchronizationOutcome.SkippedOverlapping, Assert.Single(report.Results).Outcome);
        Assert.Empty(adapter.RequestedRanges);
    }

    [Fact]
    public async Task An_expired_lock_does_not_block_the_account_for_ever()
    {
        // Un proceso que muere sin soltar el cerrojo no puede dejar la cuenta sin
        // sincronizar hasta que alguien lo note a mano.
        var world = await NewWorldAsync();

        await using (var context = fixture.CreateContext(world.Owner))
        {
            var stale = new AccountSyncLock(
                context, new FakeTimeProvider(Now.AddHours(-5)), NullLogger<AccountSyncLock>.Instance);

            await stale.TryAcquireAsync(world.AccountId, TimeSpan.FromHours(1));
        }

        var report = await RunAsync(world, new RecordingAdapter(Platform.Kraken, [Buy("TX-1")]));

        Assert.Equal(SynchronizationOutcome.Imported, Assert.Single(report.Results).Outcome);
    }

    [Fact]
    public async Task A_platform_that_fails_does_not_stop_the_others()
    {
        var world = await NewWorldAsync(withSecondPlatform: true);

        var report = await RunAsync(
            world,
            new FailingAdapter(Platform.Kraken, new InvalidOperationException("Kraken no responde")),
            new RecordingAdapter(Platform.Bit2Me, [Buy("TX-B")]));

        Assert.Equal(2, report.Results.Count);
        Assert.Equal(SynchronizationOutcome.Failed, report.Results.Single(r => r.Platform == Platform.Kraken).Outcome);
        Assert.Equal(SynchronizationOutcome.Imported, report.Results.Single(r => r.Platform == Platform.Bit2Me).Outcome);
    }

    [Fact]
    public async Task A_credential_the_platform_rejects_is_marked_invalid_and_left_out_of_the_next_run()
    {
        var world = await NewWorldAsync(withSecondPlatform: true);

        var report = await RunAsync(
            world,
            new FailingAdapter(Platform.Kraken, new Kapea.Infrastructure.Import.Kraken.KrakenApiException(["EAPI:Invalid key"])),
            new RecordingAdapter(Platform.Bit2Me, [Buy("TX-B")]));

        Assert.Equal(
            SynchronizationOutcome.CredentialInvalid,
            report.Results.Single(r => r.Platform == Platform.Kraken).Outcome);
        Assert.Equal(
            SynchronizationOutcome.Imported,
            report.Results.Single(r => r.Platform == Platform.Bit2Me).Outcome);

        await using var context = fixture.CreateContext(world.Owner);
        var credential = await context.BrokerCredentials
            .SingleAsync(entity => entity.Platform == Platform.Kraken);

        Assert.Equal(CredentialStatus.Invalid, credential.Status);
        Assert.False(credential.IsUsable);

        var next = new RecordingAdapter(Platform.Kraken, []);
        var second = await RunAsync(world, next, new RecordingAdapter(Platform.Bit2Me, []));

        Assert.DoesNotContain(second.Results, result => result.Platform == Platform.Kraken);
    }

    [Fact]
    public async Task The_run_reports_its_start_and_its_end_in_the_log()
    {
        var world = await NewWorldAsync();
        var messages = new List<string>();

        await RunAsync(world, [new RecordingAdapter(Platform.Kraken, [Buy("TX-1")])], messages);

        Assert.Contains(messages, message => message.Contains("Sincronización iniciada", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("Sincronización terminada", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("movimientos nuevos", StringComparison.Ordinal));
    }

    private async Task<SynchronizationReport> RunAsync(World world, params IApiImportAdapter[] adapters) =>
        await RunAsync(world, adapters, []);

    private async Task<SynchronizationReport> RunAsync(
        World world,
        IReadOnlyList<IApiImportAdapter> adapters,
        List<string> messages)
    {
        await using var context = fixture.CreateContext(world.Owner);
        var time = new FakeTimeProvider(Now);
        var user = new FixedUser(world.Owner);

        var pipeline = new ImportPipeline(
            new ImportRepository(context),
            new AssetCatalog(context, NullLogger<AssetCatalog>.Instance),
            new NoRates(),
            user,
            time,
            NullLogger<ImportPipeline>.Instance);

        var credentialService = new BrokerCredentialService(
            world.Secrets, [], user, time, NullLogger<BrokerCredentialService>.Instance);

        var service = new SynchronizationService(
            new SynchronizationRepository(context),
            new ImportRepository(context),
            new ImportAdapterRegistry([], adapters),
            pipeline,
            credentialService,
            new AccountSyncLock(context, time, NullLogger<AccountSyncLock>.Instance),
            time,
            new CapturingLogger<SynchronizationService>(messages));

        var report = await service.RunAsync();
        await context.SaveChangesAsync();

        return report;
    }

    private async Task<World> NewWorldAsync(bool withSecondPlatform = false)
    {
        var owner = new UserId(Guid.NewGuid());
        var secrets = new InMemorySecrets();

        await using var context = fixture.CreateContext(owner);

        var account = PlatformAccount.Create(owner, Platform.Kraken, "Kraken", Currency.Euro);
        context.Accounts.Add(account);
        context.BrokerCredentials.Add(Credential(owner, account, secrets));

        if (withSecondPlatform)
        {
            var second = PlatformAccount.Create(owner, Platform.Bit2Me, "Bit2Me", Currency.Euro);
            context.Accounts.Add(second);
            context.BrokerCredentials.Add(Credential(owner, second, secrets));
        }

        await context.SaveChangesAsync();

        return new World(owner, account.Id, secrets);
    }

    private static BrokerCredential Credential(UserId owner, PlatformAccount account, InMemorySecrets secrets)
    {
        var name = $"secret-{account.Id:N}";
        secrets.Store[name] = new ApiSecret("clave", "secreto");

        return BrokerCredential.Register(
            owner, account.Id, account.Platform, account.Platform.ToString(), name, CredentialScopes.Read, Now);
    }

    private static ImportRecord Buy(string naturalId) =>
        new(
            naturalId, null, TransactionType.Buy, "BTC", AssetClass.Crypto, 0.1m, 10000m, 1000m,
            Currency.Euro, 0m, null, new DateTimeOffset(2024, 1, 10, 10, 0, 0, TimeSpan.Zero), null, "UTC", null,
            $"raw:{naturalId}");

    private sealed record World(UserId Owner, Guid AccountId, InMemorySecrets Secrets);

    private sealed class RecordingAdapter(Platform platform, IReadOnlyList<ImportRecord> records) : IApiImportAdapter
    {
        internal List<(DateTimeOffset From, DateTimeOffset To)> RequestedRanges { get; } = [];

        public Platform Platform => platform;

        public ImportSourceKind SourceKind => ImportSourceKind.RemoteApi;

        public Task<ImportReadResult> ReadAsync(
            ApiCredential credential, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        {
            RequestedRanges.Add((from, to));

            return Task.FromResult(new ImportReadResult(records, []));
        }
    }

    private sealed class FailingAdapter(Platform platform, Exception failure) : IApiImportAdapter
    {
        public Platform Platform => platform;

        public ImportSourceKind SourceKind => ImportSourceKind.RemoteApi;

        public Task<ImportReadResult> ReadAsync(
            ApiCredential credential, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromException<ImportReadResult>(failure);
    }

    private sealed class InMemorySecrets : ISecretStore
    {
        internal Dictionary<string, ApiSecret> Store { get; } = new(StringComparer.Ordinal);

        public Task SetAsync(string name, ApiSecret secret, CancellationToken cancellationToken = default)
        {
            Store[name] = secret;

            return Task.CompletedTask;
        }

        public Task<ApiSecret?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.GetValueOrDefault(name));

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            Store.Remove(name);

            return Task.CompletedTask;
        }
    }

    private sealed class NoRates : IExchangeRateProvider
    {
        public Task<ExchangeRate?> ResolveAsync(
            Currency currency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<ExchangeRate?>(null);
    }

    private sealed class FixedUser(UserId id) : ICurrentUser
    {
        public UserId Id => id;
    }

    private sealed class CapturingLogger<T>(List<string> messages) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}
