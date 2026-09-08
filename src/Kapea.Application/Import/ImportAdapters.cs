using Kapea.Domain.Accounts;

namespace Kapea.Application.Import;

/// <summary>Forma en la que un adaptador obtiene los datos.</summary>
public enum ImportSourceKind
{
    /// <summary>Fichero que sube el usuario, porque la plataforma no ofrece API de histórico.</summary>
    UploadedFile = 1,

    /// <summary>API remota que se consulta desde el servidor con una credencial de solo lectura.</summary>
    RemoteApi = 2,
}

public interface IImportAdapter
{
    Platform Platform { get; }

    ImportSourceKind SourceKind { get; }
}

/// <summary>Adaptador de fichero. Recibe el contenido subido y devuelve registros normalizados.</summary>
public interface IFileImportAdapter : IImportAdapter
{
    Task<ImportReadResult> ReadAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Adaptador de API. Recibe la credencial y el rango temporal. Son dos puertos y no
/// uno porque forzar una firma común obligaría a inventar parámetros vacíos en ambos
/// lados; lo que sí comparten es el tipo de registro que entregan.
/// </summary>
public interface IApiImportAdapter : IImportAdapter
{
    Task<ImportReadResult> ReadAsync(
        ApiCredential credential,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Credencial ya descifrada, viva solo durante la llamada. Nunca se serializa ni
/// se registra en un log.
/// </summary>
public sealed record ApiCredential(string Key, string Secret);
