using Kapea.Domain.Common;

namespace Kapea.Domain.ValueObjects;

/// <summary>
/// Propietario de una entidad de cartera. Es un tipo propio, y no un Guid suelto,
/// para que no pueda pasarse por descuido donde se espera el identificador de otra
/// cosa. Existe desde el primer día aunque solo haya un usuario, para no migrar
/// datos si la aplicación se abre a más.
/// </summary>
public readonly record struct UserId
{
    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("El identificador de usuario no puede estar vacío.");
        }

        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();
}
