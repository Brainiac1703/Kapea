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
