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

    /// <summary>
    /// Clave del servicio. Opcional: sin ella se llama con la identidad del proceso.
    /// </summary>
    /// <remarks>
    /// Existe para poder desarrollar en local, donde no hay identidad administrada con
    /// la que pedir un token. La aplicación desplegada no la lleva.
    /// </remarks>
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

    /// <summary>Hay servicio si se sabe a dónde llamar y con qué despliegue.</summary>
    /// <remarks>
    /// La clave no cuenta: sin ella la llamada se autentica con la identidad del
    /// proceso, que es como llama la aplicación desplegada.
    /// </remarks>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(Deployment);

    /// <summary>Con clave se manda la clave; sin ella, un token de la identidad.</summary>
    public bool UsesApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
