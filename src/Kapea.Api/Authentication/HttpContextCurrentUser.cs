using System.Security.Claims;
using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Api.Authentication;

/// <summary>
/// Usuario tomado del token que valida la API.
/// </summary>
/// <remarks>
/// Nunca se acepta el identificador como parámetro de entrada. Si viniera de la
/// petición, cualquiera podría pedir los datos de otro cambiando un valor, y el filtro
/// global de la base de datos filtraría por el usuario equivocado sin protestar.
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Identificador de objeto de Entra. Es estable; el correo o el nombre no lo son.</summary>
    private static readonly string[] IdentifierClaims =
    [
        "oid",
        "http://schemas.microsoft.com/identity/claims/objectidentifier",
        ClaimTypes.NameIdentifier,
        "sub",
    ];

    public UserId Id
    {
        get
        {
            var principal = accessor.HttpContext?.User
                ?? throw new UnauthorizedAccessException("La petición no tiene usuario autenticado.");

            foreach (var claim in IdentifierClaims)
            {
                if (Guid.TryParse(principal.FindFirstValue(claim), out var value))
                {
                    return new UserId(value);
                }
            }

            throw new UnauthorizedAccessException(
                "El token no trae un identificador de usuario reconocible.");
        }
    }
}
