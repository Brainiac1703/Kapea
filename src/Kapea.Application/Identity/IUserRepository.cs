using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Identity;

public interface IUserRepository
{
    /// <summary>Busca al usuario que tenga enlazada esa identidad. Null si nadie la tiene.</summary>
    Task<User?> FindByIdentityAsync(string provider, string subject, CancellationToken cancellationToken = default);

    Task<User?> FindAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Indica si ya existe algún usuario. Se usa para la reasignación de los datos de desarrollo.</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Reasigna a un usuario todo lo que estuviera a nombre de otro identificador.</summary>
    Task<int> ReassignOwnershipAsync(UserId from, UserId to, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
