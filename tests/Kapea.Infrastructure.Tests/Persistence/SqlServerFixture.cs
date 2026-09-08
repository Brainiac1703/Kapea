using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Kapea.Infrastructure.Tests.Persistence;

/// <summary>
/// Levanta un SQL Server real en contenedor. Se usa el motor de verdad y no un
/// sustituto en memoria porque lo que hay que comprobar aquí —precisión decimal de
/// las columnas y comportamiento del filtro global— depende del propio motor.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>Imagen fijada a propósito: un latest cambiante haría que el mismo test pase hoy y falle mañana.</summary>
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext(new UserId(Guid.NewGuid()));
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync().AsTask();

    public KapeaDbContext CreateContext(UserId userId)
    {
        var options = new DbContextOptionsBuilder<KapeaDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        return new KapeaDbContext(options, new FixedUser(userId));
    }

    public string ConnectionString => _container.GetConnectionString();

    private sealed class FixedUser(UserId id) : ICurrentUser
    {
        public UserId Id => id;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql-server";
}
