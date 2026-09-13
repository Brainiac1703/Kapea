using Kapea.Application.Abstractions;
using Kapea.Domain.ValueObjects;

namespace Kapea.Api.Authentication;

/// <summary>
/// Usuario de la sesión.
/// </summary>
/// <remarks>
/// Sale de la reclamación que Kapea emite al iniciar sesión, no de la del proveedor:
/// el identificador con el que se guardan los datos es interno, y así enlazar otro
/// proveedor no cambia a quién pertenece nada.
///
/// Nunca se acepta el identificador como parámetro de entrada. Si viniera de la
/// petición, cualquiera podría pedir los datos de otro cambiando un valor, y el filtro
/// global de la base de datos filtraría por el usuario equivocado sin protestar.
/// </remarks>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public UserId Id
    {
        get
        {
            var principal = accessor.HttpContext?.User
                ?? throw new UnauthorizedAccessException("La petición no tiene usuario autenticado.");

            var value = principal.FindFirst(KapeaAuthentication.UserIdClaim)?.Value;

            return Guid.TryParse(value, out var userId)
                ? new UserId(userId)
                : throw new UnauthorizedAccessException("La sesión no identifica a ningún usuario de Kapea.");
        }
    }
}
