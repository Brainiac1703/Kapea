namespace Kapea.Infrastructure.Import.Mapping;

/// <summary>
/// Servicio que propone cómo leer un formato desconocido.
/// </summary>
/// <remarks>
/// Todo opcional. Sin endpoint configurado, Kapea funciona igual: el mapeo se hace a
/// mano, que es más lento pero no depende de nadie.
/// </remarks>
public sealed class AzureOpenAiOptions
{
    public const string SectionName = "MappingProposals:AzureOpenAi";

    public string? Endpoint { get; set; }

    public string? Deployment { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Versión de la API. Se fija en configuración para no depender de un cambio remoto.</summary>
    public string ApiVersion { get; set; } = "2024-10-21";

    /// <summary>
    /// Cuánta seguridad basta para aplicar un mapeo sin preguntar.
    /// </summary>
    /// <remarks>
    /// Es un juicio, y el valor bueno solo se sabe usándolo. Sale a configuración con un
    /// valor prudente para poder subirlo o bajarlo sin tocar código.
    /// </remarks>
    public double ConfidenceThreshold { get; set; } = 0.85;

    /// <summary>Cuánto se espera antes de renunciar. Un mapeo es un atajo, no algo por lo que bloquear una importación.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Deployment)
        && !string.IsNullOrWhiteSpace(ApiKey);
}
