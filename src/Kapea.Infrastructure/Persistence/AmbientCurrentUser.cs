using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Usuario de los procesos que no atienden peticiones. Empieza sin declarar y hay que
/// decir explícitamente en nombre de quién se actúa.
/// </summary>
/// <remarks>
/// Falla en lugar de devolver un usuario vacío: un identificador por omisión aquí
/// escribiría los movimientos de todo el mundo bajo el mismo dueño, y el filtro global
/// dejaría de aislar nada sin que nada protestara.
/// </remarks>
public sealed class AmbientCurrentUser : ICurrentUser, ICurrentUserScope
{
    private UserId? _userId;

    public UserId Id => _userId
        ?? throw new InvalidOperationException(
            "No se ha declarado en nombre de qué usuario se actúa. Llama antes a ICurrentUserScope.ActAs.");

    public void ActAs(UserId userId) => _userId = userId;
}
