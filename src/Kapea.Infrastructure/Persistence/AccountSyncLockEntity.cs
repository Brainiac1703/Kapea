namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Fila del cerrojo de sincronización. Es infraestructura pura —no representa nada del
/// negocio— y por eso vive aquí y no en el dominio.
/// </summary>
public sealed class AccountSyncLockRow
{
    public Guid AccountId { get; set; }

    public Guid Token { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
