using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Kapea.Infrastructure.Import.Bit2Me;

/// <summary>
/// Firma de las peticiones a Bit2Me: HMAC-SHA512 sobre el resumen SHA-256 del mensaje,
/// en base64. El mensaje es el nonce, la URL y, si lo hay, el cuerpo, separados por ':'.
/// </summary>
/// <remarks>
/// La documentación firma su ejemplo con una ruta sin query, así que no aclara si la
/// cadena de consulta entra en la firma. Aquí se incluye, que es la lectura literal de
/// "la URL de la petición". Si Bit2Me rechazara las peticiones paginadas por firma
/// inválida, el cambio es acotado: quitar la query en <see cref="MessageToSign"/>.
/// </remarks>
public static class Bit2MeSignature
{
    public static string MessageToSign(long nonce, string url, string? body) =>
        string.IsNullOrEmpty(body)
            ? string.Create(CultureInfo.InvariantCulture, $"{nonce}:{url}")
            : string.Create(CultureInfo.InvariantCulture, $"{nonce}:{url}:{body}");

    public static string Compute(long nonce, string url, string? body, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(MessageToSign(nonce, url, body)));

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));

        return Convert.ToBase64String(hmac.ComputeHash(digest));
    }
}
