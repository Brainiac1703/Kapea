using System.Security.Claims;
using System.Text.Encodings.Web;
using Kapea.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

namespace Kapea.Api.Tests;

/// <summary>
/// Levanta la API contra un SQL Server real y con un esquema de autenticación de
/// prueba.
/// </summary>
/// <remarks>
/// El esquema de prueba sustituye a Entra pero no relaja el aislamiento: el
/// identificador sigue saliendo de una reclamación del principal y no de un parámetro,
/// que es justo lo que estas pruebas tienen que poder comprobar.
/// </remarks>
public sealed class KapeaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    /// <summary>Usuario que presentará el cliente autenticado. Se cambia por test.</summary>
    public Guid CurrentUserId { get; set; } = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<KapeaDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>Cliente con token de prueba del usuario indicado.</summary>
    public HttpClient CreateClientFor(Guid userId)
    {
        CurrentUserId = userId;

        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, userId.ToString());

        return client;
    }

    /// <summary>Cliente sin token: se usa para comprobar que la API no responde sin autenticar.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();

    public KapeaDbContext CreateContext(Guid userId)
    {
        var options = new DbContextOptionsBuilder<KapeaDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        return new KapeaDbContext(options, new FixedUser(userId));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Kapea"] = _container.GetConnectionString(),
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }

    private sealed class FixedUser(Guid id) : Application.Abstractions.ICurrentUser
    {
        public Domain.ValueObjects.UserId Id { get; } = new(id);
    }
}

/// <summary>Autenticación de prueba: el identificador viaja en una cabecera y llega como reclamación.</summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var values)
            || !Guid.TryParse(values.FirstOrDefault(), out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim("oid", userId.ToString())], SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<KapeaApiFactory>
{
    public const string Name = "kapea-api";
}
