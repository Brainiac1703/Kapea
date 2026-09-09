namespace Kapea.Api.Authentication;

/// <summary>
/// Usuario con el que se trabaja en desarrollo cuando no hay proveedor configurado.
/// </summary>
/// <remarks>
/// No es un usuario aparte: se registra y se reconoce por el mismo camino que cualquier
/// otro, con una identidad externa de un proveedor inventado. El proveedor lleva ese
/// nombre para que no pueda colisionar con el de nadie si esta base de datos llegara a
/// usarse con un proveedor de verdad.
/// </remarks>
public sealed class DevelopmentUserOptions
{
    public const string SectionName = "Authentication:DevelopmentUser";

    /// <summary>Sujeto de la identidad: fijo, para reconocer siempre al mismo usuario.</summary>
    public const string Subject = "usuario-de-desarrollo";

    public string DisplayName { get; set; } = "Usuario de desarrollo";

    /// <summary>
    /// Identificador con el que se importó todo antes de que Kapea tuviera usuarios.
    /// El primero que entra adopta esos datos; no es el identificador del usuario de
    /// desarrollo, que se crea como cualquier otro.
    /// </summary>
    public Guid LegacyOwnerId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
