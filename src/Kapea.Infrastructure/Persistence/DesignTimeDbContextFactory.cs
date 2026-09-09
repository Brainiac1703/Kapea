using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Construye el contexto para las herramientas de migración. No se conecta a nada:
/// solo necesita el proveedor para que EF sepa qué SQL generar.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KapeaDbContext>
{
    public KapeaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KapeaDbContext>()
            .UseSqlServer("Server=(localdb);Database=Kapea;Trusted_Connection=True;")
            .Options;

        return new KapeaDbContext(options, new DesignTimeUser());
    }

    private sealed class DesignTimeUser : ICurrentUser
    {
        public UserId Id { get; } = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    }
}
