using Kapea.Application.Abstractions;
using Kapea.Application.Import;
using Kapea.Application.Portfolio;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Common;
using Kapea.Domain.Exchange;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Application.Tests.Portfolio;

public class ManualMovementServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly PlatformAccount Account = PlatformAccount.Create(Owner, PlatformCode.Kraken, "Kraken", Currency.Euro);

    [Fact]
    public async Task A_manual_movement_is_registered_without_a_note()
    {
        var repository = new InMemoryRepository(Account);

        var movement = await Service(repository).RegisterAsync(Buy());

        Assert.Equal(TransactionOrigin.Manual, movement.Origin);
        Assert.Null(movement.Note);
        Assert.Equal(Now, movement.RegisteredAt);
        Assert.Contains(movement, repository.Transactions);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task A_movement_in_dollars_freezes_the_rate_of_its_date()
    {
        var repository = new InMemoryRepository(Account);

        var movement = await Service(repository).RegisterAsync(Buy(currency: "USD"));

        Assert.NotNull(movement.AppliedExchangeRate);
        Assert.Equal(new DateOnly(2026, 9, 3), movement.AppliedExchangeRate!.RequestedDate);
    }

    [Fact]
    public async Task Without_a_rate_for_the_date_the_movement_is_rejected()
    {
        var repository = new InMemoryRepository(Account);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => Service(repository, rates: false).RegisterAsync(Buy(currency: "USD")));

        Assert.Contains("tipo de cambio", exception.Message, StringComparison.Ordinal);
        Assert.Empty(repository.Transactions);
    }

    [Fact]
    public async Task Someone_elses_account_behaves_as_if_it_did_not_exist()
    {
        var repository = new InMemoryRepository();

        await Assert.ThrowsAsync<ImportTargetException>(() => Service(repository).RegisterAsync(Buy()));
    }

    [Fact]
    public async Task Revising_a_manual_movement_resolves_the_rate_again()
    {
        var repository = new InMemoryRepository(Account);
        var service = Service(repository);
        var movement = await service.RegisterAsync(Buy());

        await service.ReviseAsync(movement.Id, Buy(currency: "USD", quantity: 3m));

        Assert.Equal(new Quantity(3m), movement.Quantity);
        Assert.NotNull(movement.AppliedExchangeRate);
        Assert.Equal(Now, movement.RevisedAt);
    }

    [Fact]
    public async Task An_imported_movement_is_not_revised_nor_deleted()
    {
        var imported = Imported();
        var repository = new InMemoryRepository(Account, imported);
        var service = Service(repository);

        await Assert.ThrowsAsync<DomainException>(() => service.ReviseAsync(imported.Id, Buy()));
        await Assert.ThrowsAsync<DomainException>(() => service.DeleteAsync(imported.Id));

        Assert.Contains(imported, repository.Transactions);
    }

    [Fact]
    public async Task A_manual_movement_is_deleted()
    {
        var repository = new InMemoryRepository(Account);
        var service = Service(repository);
        var movement = await service.RegisterAsync(Buy());

        await service.DeleteAsync(movement.Id);

        Assert.DoesNotContain(movement, repository.Transactions);
    }

    [Fact]
    public async Task Voiding_and_restoring_an_imported_movement()
    {
        var imported = Imported();
        var repository = new InMemoryRepository(Account, imported);
        var service = Service(repository);

        await service.VoidAsync(imported.Id, "duplicada");
        Assert.True(imported.IsVoided);

        await service.RestoreAsync(imported.Id);
        Assert.False(imported.IsVoided);
    }

    [Fact]
    public async Task A_movement_in_a_confirmed_transfer_is_not_voided()
    {
        var imported = Imported();
        var transfer = Guid.NewGuid();
        var repository = new InMemoryRepository(Account, imported) { ConfirmedTransfers = { [imported.Id] = transfer } };

        var exception = await Assert.ThrowsAsync<MovementInConfirmedTransferException>(
            () => Service(repository).VoidAsync(imported.Id, "duplicada"));

        Assert.Equal(transfer, exception.TransferId);
        Assert.False(imported.IsVoided);
    }

    [Fact]
    public async Task Correcting_voids_the_import_and_registers_the_adjustment_in_one_save()
    {
        var imported = Imported();
        var repository = new InMemoryRepository(Account, imported);

        var adjustment = await Service(repository).CorrectAsync(imported.Id, Buy(quantity: 1.05m, text: "la cantidad venía mal"));

        Assert.True(imported.IsVoided);
        Assert.Equal("la cantidad venía mal", imported.VoidReason);
        Assert.Equal(TransactionOrigin.ManualAdjustment, adjustment.Origin);
        Assert.Equal("la cantidad venía mal", adjustment.AdjustmentReason);
        Assert.Equal(new Quantity(1.05m), adjustment.Quantity);
        Assert.Equal(1, repository.Saves);
    }

    [Fact]
    public async Task A_correction_that_breaks_the_rules_leaves_the_import_in_force()
    {
        var imported = Imported();
        var repository = new InMemoryRepository(Account, imported);

        await Assert.ThrowsAsync<DomainException>(
            () => Service(repository).CorrectAsync(imported.Id, Buy(quantity: 0m, text: "motivo")));

        Assert.False(imported.IsVoided);
        Assert.Equal(0, repository.Saves);
        Assert.Single(repository.Transactions);
    }

    [Fact]
    public async Task A_correction_without_a_reason_is_rejected()
    {
        var imported = Imported();
        var repository = new InMemoryRepository(Account, imported);

        await Assert.ThrowsAsync<DomainException>(() => Service(repository).CorrectAsync(imported.Id, Buy(text: " ")));

        Assert.False(imported.IsVoided);
    }

    [Fact]
    public async Task Deleting_the_adjustment_does_not_restore_the_import()
    {
        // Puede que el importado estuviera mal y no haya que sustituirlo por nada.
        var imported = Imported();
        var repository = new InMemoryRepository(Account, imported);
        var service = Service(repository);
        var adjustment = await service.CorrectAsync(imported.Id, Buy(text: "motivo"));

        await service.DeleteAsync(adjustment.Id);

        Assert.True(imported.IsVoided);
    }

    private static ManualMovementInput Buy(string currency = "EUR", decimal quantity = 2m, string? text = null) =>
        new(Account.Id, TransactionType.Buy, "BTC", AssetClass.Crypto, quantity, 10m, quantity * 10m, currency, 0m,
            new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero), "Europe/Madrid", text);

    private static Transaction Imported() =>
        Transaction.Imported(
            Owner, Account.Id, TransactionType.Buy, Btc.Id, new Quantity(1.5m), Money.Euros(10m), Money.Euros(15m),
            Money.Euros(0m), Occurrence.FromOffset(Now, "UTC"),
            TransactionSource.FromImport(Guid.NewGuid(), "txid", null, Guid.NewGuid().ToString("N")));

    private static readonly Asset Btc = Asset.Create("BTC", AssetClass.Crypto);

    private static ManualMovementService Service(InMemoryRepository repository, bool rates = true) =>
        new(repository, new FixedCatalog(), new FixedRates(rates), new FixedUser(), new FakeTimeProvider(Now),
            NullLogger<ManualMovementService>.Instance);

    private sealed class InMemoryRepository(PlatformAccount? account = null, params Transaction[] seeded) : IManualMovementRepository
    {
        public List<Transaction> Transactions { get; } = [.. seeded];

        public Dictionary<Guid, Guid> ConfirmedTransfers { get; } = [];

        public int Saves { get; private set; }

        public Task<PlatformAccount?> FindAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(account?.Id == accountId ? account : null);

        public Task<Transaction?> FindTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Transactions.SingleOrDefault(t => t.Id == transactionId));

        public Task<Guid?> FindConfirmedTransferAsync(Guid transactionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConfirmedTransfers.TryGetValue(transactionId, out var transfer) ? (Guid?)transfer : null);

        public void Add(Transaction transaction) => Transactions.Add(transaction);

        public void Remove(Transaction transaction) => Transactions.Remove(transaction);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;

            return Task.CompletedTask;
        }
    }

    private sealed class FixedCatalog : IAssetCatalog
    {
        public Task<Asset> ResolveAsync(string symbol, AssetClass assetClass, CancellationToken cancellationToken = default) =>
            Task.FromResult(Btc);
    }

    private sealed class FixedRates(bool available) : IExchangeRateProvider
    {
        public Task<ExchangeRate?> ResolveAsync(Currency currency, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(available ? ExchangeRate.Create(currency, 1.1m, date, date, "BCE") : null);
    }

    private sealed class FixedUser : ICurrentUser
    {
        public UserId Id => Owner;
    }
}
