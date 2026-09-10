using Kapea.Application.Abstractions;
using Kapea.Application.Credentials;
using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.Credentials;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Kapea.Application.Tests.Credentials;

public class BrokerCredentialServiceTests
{
    private static readonly ApiSecret Secret = new("clave", "secreto-muy-privado");
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserId Owner = new(Guid.NewGuid());

    /// <summary>La cuenta del usuario de estas pruebas. Registrar exige que sea suya.</summary>
    private static readonly PlatformAccount Account =
        PlatformAccount.Create(Owner, PlatformCode.Kraken, "Kraken", Currency.Euro);

    [Fact]
    public async Task A_valid_read_only_credential_is_registered_and_usable()
    {
        var store = new InMemorySecretStore();
        var credential = await Service(store).RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);

        Assert.True(credential.IsUsable);
        Assert.Equal(CredentialScopes.Read, credential.Scopes);
        Assert.Equal(Now, credential.CreatedAt);
        Assert.Equal(Secret, await store.GetAsync(credential.SecretName));
    }

    [Fact]
    public async Task A_credential_the_platform_rejects_is_not_stored()
    {
        var store = new InMemorySecretStore();
        var service = Service(store, CredentialVerification.Rejected("EAPI:Invalid key"));

        var exception = await Assert.ThrowsAsync<CredentialRejectedException>(
            () => service.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret));

        Assert.Contains("EAPI:Invalid key", exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.Names);
    }

    [Fact]
    public async Task A_credential_with_trading_or_withdrawal_scope_is_rejected()
    {
        var store = new InMemorySecretStore();
        var service = Service(store, CredentialVerification.Valid(CredentialScopes.Read | CredentialScopes.Trade));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => service.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret));

        Assert.Contains("solo lectura", exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.Names);
    }

    [Fact]
    public async Task Rotating_replaces_the_secret_and_keeps_the_credential_identity()
    {
        var store = new InMemorySecretStore();
        var service = Service(store);
        var credential = await service.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);
        var newSecret = new ApiSecret("clave", "otro-secreto");

        await service.RotateAsync(credential, newSecret);

        Assert.Equal(newSecret, await store.GetAsync(credential.SecretName));
        Assert.Equal(Now, credential.RotatedAt);
        Assert.Single(store.Names);
    }

    [Fact]
    public async Task A_revoked_credential_stops_being_usable_and_its_secret_disappears()
    {
        var store = new InMemorySecretStore();
        var service = Service(store);
        var credential = await service.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);

        await service.RevokeAsync(credential);

        Assert.False(credential.IsUsable);
        Assert.Empty(store.Names);
        Assert.Null(await service.ResolveAsync(credential));
    }

    [Fact]
    public async Task A_platform_marked_invalid_is_left_out_but_keeps_what_it_imported()
    {
        var credential = await Service(new InMemorySecretStore())
            .RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);

        credential.MarkSynchronized(Now);
        credential.MarkInvalid("EAPI:Invalid key");

        Assert.False(credential.IsUsable);
        Assert.Equal(CredentialStatus.Invalid, credential.Status);
        Assert.Equal("EAPI:Invalid key", credential.InvalidReason);
        Assert.Equal(Now, credential.LastSynchronizedAt);
    }

    [Fact]
    public async Task A_revoked_credential_is_not_reactivated_by_a_platform_rejection()
    {
        var credential = await Service(new InMemorySecretStore())
            .RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);

        credential.Revoke();
        credential.MarkInvalid("EAPI:Invalid key");

        Assert.Equal(CredentialStatus.Revoked, credential.Status);
    }

    [Fact]
    public async Task A_revoked_credential_cannot_be_rotated()
    {
        var service = Service(new InMemorySecretStore());
        var credential = await service.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);

        await service.RevokeAsync(credential);

        await Assert.ThrowsAsync<DomainException>(() => service.RotateAsync(credential, Secret));
    }

    [Fact]
    public async Task No_log_message_of_the_credential_flows_contains_the_secret()
    {
        // El registro es la vía más fácil de filtrar un secreto sin darse cuenta, así
        // que se recorre lo que emiten los flujos completos y se comprueba de verdad.
        var messages = new List<string>();
        var store = new InMemorySecretStore();
        var service = Service(store, logMessages: messages);

        var credential = await service.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);
        await service.RotateAsync(credential, new ApiSecret("clave", "otro-secreto"));
        await service.RevokeAsync(credential);

        var rejecting = Service(store, CredentialVerification.Rejected("EAPI:Invalid key"), messages);
        await Assert.ThrowsAsync<CredentialRejectedException>(
            () => rejecting.RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret));

        Assert.NotEmpty(messages);

        foreach (var secret in new[] { "secreto-muy-privado", "otro-secreto", "clave" })
        {
            Assert.DoesNotContain(messages, message => message.Contains(secret, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void The_secret_never_leaks_through_its_own_string_representation()
    {
        var text = Secret.ToString();

        Assert.DoesNotContain("secreto-muy-privado", text, StringComparison.Ordinal);
        Assert.DoesNotContain("clave", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registering_a_credential_on_someone_elses_account_is_refused()
    {
        // Sin esto, el secreto se guardaría bajo el nombre de la cuenta ajena y pisaría
        // la clave de su dueño: su siguiente sincronización usaría una clave que no es
        // suya.
        var otra = PlatformAccount.Create(
            new UserId(Guid.NewGuid()), PlatformCode.Kraken, "Kraken de otro", Currency.Euro);

        var store = new InMemorySecretStore();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Service(store).RegisterAsync(otra, PlatformCode.Kraken, "Kraken", Secret));

        Assert.Empty(store.Names);
    }

    [Fact]
    public async Task Two_people_never_share_an_entry_in_the_store()
    {
        // El nombre del secreto lleva el usuario además de la cuenta. Con solo la cuenta,
        // dos entradas podrían coincidir y una clave pisaría a la otra en silencio.
        var store = new InMemorySecretStore();

        var mine = await Service(store).RegisterAsync(Account, PlatformCode.Kraken, "Kraken", Secret);

        var otherOwner = new UserId(Guid.NewGuid());
        var theirs = PlatformAccount.Create(otherOwner, PlatformCode.Kraken, "Kraken", Currency.Euro);
        var theirSecret = new ApiSecret("otra-clave", "otro-secreto");

        var other = await Service(store, currentUser: otherOwner)
            .RegisterAsync(theirs, PlatformCode.Kraken, "Kraken", theirSecret);

        Assert.NotEqual(mine.SecretName, other.SecretName);
        Assert.Equal(Secret, await store.GetAsync(mine.SecretName));
        Assert.Equal(theirSecret, await store.GetAsync(other.SecretName));
    }

    [Fact]
    public async Task The_name_of_a_secret_reveals_neither_the_key_nor_the_alias()
    {
        // El nombre viaja en la fila de la credencial, que sí se consulta. Lo que no
        // lleva no se puede filtrar por ahí.
        var store = new InMemorySecretStore();
        var credential = await Service(store).RegisterAsync(Account, PlatformCode.Kraken, "Mi Kraken", Secret);

        Assert.DoesNotContain("clave", credential.SecretName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secreto", credential.SecretName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mi Kraken", credential.SecretName, StringComparison.OrdinalIgnoreCase);
    }

    private static BrokerCredentialService Service(
        InMemorySecretStore store,
        CredentialVerification? verification = null,
        List<string>? logMessages = null,
        UserId? currentUser = null) =>
        new(
            store,
            [new FakeVerifier(PlatformCode.Kraken, verification ?? CredentialVerification.Valid(CredentialScopes.Read))],
            new FixedUser(currentUser),
            new FakeTimeProvider(Now),
            new CapturingLogger<BrokerCredentialService>(logMessages ?? []));

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, ApiSecret> _secrets = new(StringComparer.Ordinal);

        internal IReadOnlyCollection<string> Names => _secrets.Keys;

        public Task SetAsync(string name, ApiSecret secret, CancellationToken cancellationToken = default)
        {
            _secrets[name] = secret;

            return Task.CompletedTask;
        }

        public Task<ApiSecret?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.GetValueOrDefault(name));

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            _secrets.Remove(name);

            return Task.CompletedTask;
        }
    }

    private sealed class FakeVerifier(PlatformCode platform, CredentialVerification verification) : ICredentialVerifier
    {
        public PlatformCode Platform => platform;

        public Task<CredentialVerification> VerifyAsync(ApiSecret secret, CancellationToken cancellationToken = default) =>
            Task.FromResult(verification);
    }

    private sealed class FixedUser(UserId? id = null) : ICurrentUser
    {
        public UserId Id { get; } = id ?? Owner;
    }

    private sealed class CapturingLogger<T>(List<string> messages) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}
