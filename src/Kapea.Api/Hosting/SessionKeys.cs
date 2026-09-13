extern alias AzureIdentity;

using Microsoft.AspNetCore.DataProtection;

namespace Kapea.Api.Hosting;

/// <summary>Dónde acaban las claves que cifran la cookie de sesión.</summary>
public enum SessionKeyStorage
{
    /// <summary>En memoria: un reinicio cierra la sesión de quien estuviera dentro.</summary>
    Memory,

    /// <summary>En un directorio del disco, que en local es un volumen.</summary>
    FileSystem,

    /// <summary>En un blob, que es el único sitio que sobrevive a una revisión nueva.</summary>
    Blob,
}

/// <summary>
/// Configura dónde se guardan las claves de protección de datos.
/// </summary>
/// <remarks>
/// Son tres sitios y no dos porque los tres escenarios existen: en Azure cada revisión
/// es un contenedor nuevo, así que las claves tienen que estar en un blob o publicar
/// echaría al usuario de su sesión; en local basta un volumen; y sin nada configurado la
/// aplicación tiene que arrancar igual, porque negarse a funcionar por no saber dónde
/// guardar unas claves sería peor que perder la sesión al reiniciar.
/// </remarks>
public static class SessionKeys
{
    public const string BlobUriKey = "DataProtection:BlobUri";
    public const string KeyUriKey = "DataProtection:KeyUri";
    public const string KeysPathKey = "DataProtection:KeysPath";

    /// <summary>
    /// Nombre de la aplicación con el que se derivan las claves.
    /// </summary>
    /// <remarks>
    /// Fijo y no derivado del nombre del contenedor: si cambiara entre revisiones, las
    /// claves guardadas dejarían de servir para descifrar la cookie anterior, que es
    /// justo lo que se quiere evitar.
    /// </remarks>
    private const string ApplicationName = "Kapea";

    public static SessionKeyStorage AddKapeaSessionKeys(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration[BlobUriKey] is { Length: > 0 } blobUri)
        {
            // La misma cadena de credenciales que usan la base de datos y el almacén de
            // secretos: en Azure resuelve la identidad de la aplicación, y en una máquina
            // de desarrollo la sesión de az.
            var credential = new AzureIdentity::Azure.Identity.DefaultAzureCredential();

            var builder = services
                .AddDataProtection()
                .PersistKeysToAzureBlobStorage(new Uri(blobUri), credential)
                .SetApplicationName(ApplicationName);

            // Cifrar las claves con una clave del almacén es lo que impide que quien
            // pueda leer el blob pueda además descifrar las cookies. Es opcional para no
            // exigir un almacén a quien solo quiera el blob.
            if (configuration[KeyUriKey] is { Length: > 0 } keyUri)
            {
                builder.ProtectKeysWithAzureKeyVault(new Uri(keyUri), credential);
            }

            return SessionKeyStorage.Blob;
        }

        if (configuration[KeysPathKey] is { Length: > 0 } keysPath)
        {
            services
                .AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName(ApplicationName);

            return SessionKeyStorage.FileSystem;
        }

        return SessionKeyStorage.Memory;
    }
}
