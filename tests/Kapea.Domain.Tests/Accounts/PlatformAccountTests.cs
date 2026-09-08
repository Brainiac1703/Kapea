using Kapea.Domain.Accounts;
using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Tests.Accounts;

public class PlatformAccountTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());

    [Fact]
    public void An_account_keeps_its_platform_alias_and_base_currency()
    {
        var account = PlatformAccount.Create(Owner, Platform.Kraken, "  Kraken principal  ", Currency.Euro);

        Assert.Equal(Platform.Kraken, account.Platform);
        Assert.Equal("Kraken principal", account.Alias);
        Assert.Equal(Currency.Euro, account.BaseCurrency);
        Assert.Equal(Owner, account.UserId);
    }

    [Fact]
    public void An_account_without_alias_is_rejected() =>
        Assert.Throws<DomainException>(() => PlatformAccount.Create(Owner, Platform.Xtb, "   ", Currency.Euro));

    [Fact]
    public void Deleting_an_account_with_transactions_is_rejected()
    {
        var account = PlatformAccount.Create(Owner, Platform.Xtb, "XTB", Currency.Euro);

        var exception = Assert.Throws<DomainException>(() => account.EnsureCanBeDeleted(transactionCount: 3));

        Assert.Contains("3 movimientos", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deleting_an_empty_account_is_allowed()
    {
        var account = PlatformAccount.Create(Owner, Platform.Xtb, "XTB", Currency.Euro);

        account.EnsureCanBeDeleted(transactionCount: 0);
    }

    [Fact]
    public void An_empty_user_id_is_rejected() =>
        Assert.Throws<DomainException>(() => new UserId(Guid.Empty));
}
