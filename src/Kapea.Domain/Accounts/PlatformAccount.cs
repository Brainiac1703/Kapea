using Kapea.Domain.Common;
using Kapea.Domain.ValueObjects;

namespace Kapea.Domain.Accounts;

/// <summary>Cuenta del usuario en una plataforma. Todo movimiento pertenece a una y solo una.</summary>
public sealed class PlatformAccount
{
    private PlatformAccount(Guid id, UserId userId, Platform platform, string alias, Currency baseCurrency)
    {
        Id = id;
        UserId = userId;
        Platform = platform;
        Alias = alias;
        BaseCurrency = baseCurrency;
    }

    public Guid Id { get; }

    public UserId UserId { get; }

    public Platform Platform { get; }

    public string Alias { get; private set; }

    public Currency BaseCurrency { get; }

    public static PlatformAccount Create(UserId userId, Platform platform, string alias, Currency baseCurrency)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DomainException("Una cuenta necesita alias para distinguirla de otras de la misma plataforma.");
        }

        return new PlatformAccount(Guid.NewGuid(), userId, platform, alias.Trim(), baseCurrency);
    }

    public void Rename(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new DomainException("Una cuenta necesita alias para distinguirla de otras de la misma plataforma.");
        }

        Alias = alias.Trim();
    }

    /// <summary>
    /// Borrar una cuenta con movimientos dejaría lotes y resultados huérfanos y rompería
    /// la trazabilidad hasta el origen, así que se exige vaciarla o reasignarla antes.
    /// </summary>
    public void EnsureCanBeDeleted(int transactionCount)
    {
        if (transactionCount > 0)
        {
            throw new DomainException(
                $"La cuenta '{Alias}' tiene {transactionCount} movimientos: elimina o reasigna sus movimientos antes de borrarla.");
        }
    }
}
