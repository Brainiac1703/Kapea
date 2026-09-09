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
    public string Platform { get; set; } = "Xtb";

    public string Alias { get; set; } = string.Empty;

    public string BaseCurrency { get; set; } = "EUR";

    public bool IsComplete => !string.IsNullOrWhiteSpace(Alias);
}

/// <summary>Alta o rotación de una credencial. El secreto vive solo mientras dura el envío.</summary>
public sealed class BrokerCredentialModel
{
    public Guid AccountId { get; set; }

    public string Platform { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>En una rotación el alias no se pide: se conserva el de la credencial.</summary>
    public bool IsRotation { get; set; }

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret)
        && (IsRotation || (!string.IsNullOrWhiteSpace(Alias) && AccountId != Guid.Empty));
}
