using Kapea.Domain.Identity;
using Kapea.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Kapea.Application.Identity;

/// <summary>
/// Traslada al primer usuario que entra lo que se importó antes de que Kapea tuviera
/// usuarios, cuando todo se atribuía a un identificador fijo de desarrollo.
/// </summary>
/// <remarks>
/// Solo es correcto porque hay una única persona. Con dos usuarios, el primero en
/// entrar se quedaría con los datos del otro, así que la adopción se hace una sola vez
/// y únicamente si él es el único usuario del registro.
/// </remarks>
public sealed class DevelopmentDataAdoption(
    IUserRepository repository,
    ILogger<DevelopmentDataAdoption> logger)
{
    public async Task AdoptAsync(User user, UserId previousOwner, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Id == previousOwner)
        {
            return;
        }

        // Si ya hay más de un usuario, la adopción dejaría de ser inocua: podría
        // llevarse datos que pertenecen a otra persona.
        if (await HasOtherUsersAsync(user, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var moved = await repository
            .ReassignOwnershipAsync(previousOwner, user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (moved > 0)
        {
            logger.LogInformation(
                "Adoptados {Filas} registros del usuario de desarrollo por el usuario {Usuario}.", moved, user.Id);
        }
    }

    private async Task<bool> HasOtherUsersAsync(User user, CancellationToken cancellationToken) =>
        await repository.CountAsync(cancellationToken).ConfigureAwait(false) > 1;
}
