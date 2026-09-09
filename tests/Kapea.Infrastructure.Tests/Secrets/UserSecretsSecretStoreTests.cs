using Kapea.Application.Abstractions;
using Kapea.Infrastructure.Secrets;

namespace Kapea.Infrastructure.Tests.Secrets;

public class UserSecretsSecretStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"kapea-{Guid.NewGuid():N}", "secrets.json");

    [Fact]
    public async Task A_stored_secret_is_read_back_whole()
    {
        var store = new UserSecretsSecretStore(_path);
        var secret = new ApiSecret("clave", "secreto");

        await store.SetAsync("broker-kraken-1", secret);

        Assert.Equal(secret, await store.GetAsync("broker-kraken-1"));
    }

    [Fact]
    public async Task A_secret_that_does_not_exist_comes_back_as_nothing()
    {
        var store = new UserSecretsSecretStore(_path);

        Assert.Null(await store.GetAsync("no-existe"));
    }

    [Fact]
    public async Task Storing_a_second_secret_does_not_lose_the_first()
    {
        var store = new UserSecretsSecretStore(_path);

        await store.SetAsync("uno", new ApiSecret("k1", "s1"));
        await store.SetAsync("dos", new ApiSecret("k2", "s2"));

        Assert.Equal("s1", (await store.GetAsync("uno"))!.Secret);
        Assert.Equal("s2", (await store.GetAsync("dos"))!.Secret);
    }

    [Fact]
    public async Task Rotating_overwrites_the_previous_secret()
    {
        var store = new UserSecretsSecretStore(_path);

        await store.SetAsync("uno", new ApiSecret("k", "viejo"));
        await store.SetAsync("uno", new ApiSecret("k", "nuevo"));

        Assert.Equal("nuevo", (await store.GetAsync("uno"))!.Secret);
        Assert.DoesNotContain("viejo", await File.ReadAllTextAsync(_path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleting_removes_the_secret_from_the_file()
    {
        var store = new UserSecretsSecretStore(_path);

        await store.SetAsync("uno", new ApiSecret("k", "s"));
        await store.DeleteAsync("uno");

        Assert.Null(await store.GetAsync("uno"));
        Assert.DoesNotContain("\"s\"", await File.ReadAllTextAsync(_path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleting_something_that_is_not_there_is_not_an_error()
    {
        var store = new UserSecretsSecretStore(_path);

        await store.DeleteAsync("no-existe");
    }

    [Fact]
    public void Secrets_live_outside_the_project_tree_so_they_cannot_be_committed()
    {
        var path = UserSecretsSecretStore.DefaultPathFor("11111111-2222-3333-4444-555555555555");

        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path, StringComparison.Ordinal);
        Assert.Contains(Path.Combine(".microsoft", "usersecrets"), path, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path)!;

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
