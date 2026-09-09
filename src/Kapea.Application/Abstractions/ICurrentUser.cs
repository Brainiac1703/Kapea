using Kapea.Domain.ValueObjects;

namespace Kapea.Application.Abstractions;

/// <summary>
/// Usuario en cuyo nombre se ejecuta la operación. Sale siempre del token que valida
/// la API y nunca de un parámetro de entrada: aceptarlo por parámetro convertiría el
/// aislamiento entre usuarios en una convención en vez de una garantía.
/// </summary>
public interface ICurrentUser
{
    UserId Id { get; }
}

/// <summary>
/// Permite que un proceso sin petición HTTP declare en nombre de quién actúa.
/// </summary>
/// <remarks>
/// Lo necesita la sincronización programada: no hay token del que sacar el usuario, y
/// sin declararlo el filtro global de la base de datos filtraría por un usuario vacío
/// y los movimientos importados quedarían sin dueño. Cada cuenta se procesa declarando
/// antes a quién pertenece.
/// </remarks>
public interface ICurrentUserScope
{
    void ActAs(UserId userId);
}
