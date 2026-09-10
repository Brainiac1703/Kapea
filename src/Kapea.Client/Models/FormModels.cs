using Kapea.Shared.Contracts;

namespace Kapea.Client.Models;

/// <summary>
/// Modelos de formulario.
/// </summary>
/// <remarks>
/// Son tipos aparte de los contratos de la API a propósito. Los contratos son
/// inmutables —lo que se envía no debe poder cambiar a mitad— y el enlace bidireccional
/// de un formulario necesita escribir campo a campo. Además, un formulario tiene
/// estados intermedios que no son un contrato válido: a medio rellenar.
/// </remarks>
public sealed class NewAccountModel
{
    /// <summary>
    /// Plataformas entre las que elegir.
    /// </summary>
    /// <remarks>
    /// Viajan dentro del modelo por lo mismo que las cuentas de una credencial: el
    /// diálogo de Fluent UI solo recibe su Content, y cualquier otro parámetro llegaría
    /// sin asignar dejando el desplegable vacío sin que nada falle.
    /// </remarks>
    public IReadOnlyList<PlatformResponse> AvailablePlatforms { get; init; } = [];

    public string Platform { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public string BaseCurrency { get; set; } = "EUR";

    public bool IsComplete => Missing.Count == 0;

    /// <summary>Qué falta por rellenar, con la clave de su etiqueta.</summary>
    public IReadOnlyList<string> Missing
    {
        get
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(Platform))
            {
                missing.Add("Common_Platform");
            }

            if (string.IsNullOrWhiteSpace(Alias))
            {
                missing.Add("Common_Alias");
            }

            return missing;
        }
    }
}

/// <summary>Alta o rotación de una credencial. El secreto vive solo mientras dura el envío.</summary>
public sealed class BrokerCredentialModel
{
    /// <summary>
    /// Cuentas entre las que elegir.
    /// </summary>
    /// <remarks>
    /// Viajan dentro del modelo y no como parámetro del componente porque el diálogo de
    /// Fluent UI solo recibe su Content: cualquier otro parámetro se queda sin asignar y
    /// el desplegable aparece vacío sin que nada falle.
    /// </remarks>
    public IReadOnlyList<AccountResponse> AvailableAccounts { get; init; } = [];

    public Guid AccountId { get; set; }

    public string Platform { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>En una rotación el alias no se pide: se conserva el de la credencial.</summary>
    public bool IsRotation { get; set; }

    public bool IsComplete => Missing.Count == 0;

    /// <summary>
    /// Qué falta por rellenar, con la clave de su etiqueta.
    /// </summary>
    /// <remarks>
    /// Se enumera en lugar de devolver solo un sí o un no porque un formulario que se
    /// cierra sin hacer nada y sin decir por qué es indistinguible de uno que ha
    /// funcionado: se ve que no aparece el resultado, pero no qué faltaba.
    /// </remarks>
    public IReadOnlyList<string> Missing
    {
        get
        {
            var missing = new List<string>();

            if (!IsRotation && AccountId == Guid.Empty)
            {
                missing.Add("Credentials_Account");
            }

            if (!IsRotation && string.IsNullOrWhiteSpace(Alias))
            {
                missing.Add("Common_Alias");
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                missing.Add("Credentials_ApiKey");
            }

            if (string.IsNullOrWhiteSpace(ApiSecret))
            {
                missing.Add("Credentials_ApiSecret");
            }

            return missing;
        }
    }
}
