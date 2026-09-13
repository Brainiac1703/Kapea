using Kapea.Application.Import;
using Kapea.Domain.Accounts;

namespace Kapea.Infrastructure.Import.Mapping;

/// <summary>
/// El que se registra cuando no hay servicio configurado.
/// </summary>
/// <remarks>
/// Existe para que el resto del código no tenga que preguntar si hay servicio antes de
/// cada llamada. Devolver nada es una respuesta válida: significa que hay que mapear a
/// mano, y esa vía siempre está.
/// </remarks>
public sealed class UnavailableMappingProposer : IMappingProposer
{
    public bool IsAvailable => false;

    public Task<MappingProposal?> ProposeAsync(
        PlatformCode platform,
        MappingSample sample,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MappingProposal?>(null);
}
