using Kapea.Domain.Accounts;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public class UserIsolationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_query_that_forgets_to_filter_still_sees_only_its_own_data()
    {
        var (mine, theirs) = await TwoAccountsAsync();

        await using var context = fixture.CreateContext(mine.UserId);
        var accounts = await context.Accounts.ToListAsync();

        Assert.Contains(accounts, account => account.Id == mine.Id);
        Assert.DoesNotContain(accounts, account => account.Id == theirs.Id);
    }

    [Fact]
    public async Task Asking_for_someone_elses_entity_by_id_behaves_as_if_it_did_not_exist()
    {
        var (mine, theirs) = await TwoAccountsAsync();

        await using var context = fixture.CreateContext(mine.UserId);

        Assert.Null(await context.Accounts.SingleOrDefaultAsync(account => account.Id == theirs.Id));
    }

    [Fact]
    public async Task Counting_does_not_leak_the_existence_of_other_users_data()
    {
        var (mine, _) = await TwoAccountsAsync();

        await using var context = fixture.CreateContext(mine.UserId);

        Assert.Equal(1, await context.Accounts.CountAsync(account => account.Alias == "Cuenta de prueba"));
    }

    [Fact]
    public async Task The_catalogue_of_assets_is_shared_because_it_holds_nobodys_data()
    {
        var symbol = "CAT" + Random.Shared.Next(100000);
        var owner = new UserId(Guid.NewGuid());

        await using (var writer = fixture.CreateContext(owner))
        {
            writer.Assets.Add(Domain.Assets.Asset.Create(symbol, Domain.Assets.AssetClass.Crypto));
            await writer.SaveChangesAsync();
        }

        await using var reader = fixture.CreateContext(new UserId(Guid.NewGuid()));

        Assert.NotNull(await reader.Assets.SingleOrDefaultAsync(asset => asset.CanonicalSymbol == symbol));
    }

    private async Task<(PlatformAccount Mine, PlatformAccount Theirs)> TwoAccountsAsync()
    {
        var mine = PlatformAccount.Create(new UserId(Guid.NewGuid()), Platform.Kraken, "Cuenta de prueba", Currency.Euro);
        var theirs = PlatformAccount.Create(new UserId(Guid.NewGuid()), Platform.Kraken, "Cuenta de prueba", Currency.Euro);

        await using (var context = fixture.CreateContext(mine.UserId))
        {
            context.Accounts.Add(mine);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext(theirs.UserId))
        {
            context.Accounts.Add(theirs);
            await context.SaveChangesAsync();
        }

        return (mine, theirs);
    }
}
