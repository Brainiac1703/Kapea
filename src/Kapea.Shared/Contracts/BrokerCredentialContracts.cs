namespace Kapea.Shared.Contracts;

/// <summary>
/// Lo que el cliente ve de una credencial: metadatos y nada más.
/// </summary>
/// <remarks>
/// No tiene ningún campo capaz de transportar el secreto, ni siquiera enmascarado.
/// Filtrarlo exigiría cambiar este contrato a propósito, que es justo la barrera que
/// se busca: el cliente WebAssembly corre en el navegador del usuario.
/// </remarks>
public sealed record BrokerCredentialResponse(
    Guid Id,
    Guid AccountId,
    string Platform,
    string Alias,
    string Scopes,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RotatedAt,
    DateTimeOffset? LastSynchronizedAt,
    string? InvalidReason);

/// <summary>Alta de credencial. Es el único contrato por el que el secreto entra, y solo va del cliente al servidor.</summary>
public sealed record RegisterBrokerCredentialRequest(
    Guid AccountId,
    string Platform,
    string Alias,
    string ApiKey,
    string ApiSecret);

/// <summary>Rotación: sustituye el secreto de una credencial existente.</summary>
public sealed record RotateBrokerCredentialRequest(string ApiKey, string ApiSecret);
