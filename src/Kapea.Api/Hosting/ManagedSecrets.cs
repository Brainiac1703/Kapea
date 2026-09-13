extern alias AzureIdentity;

using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace Kapea.Api.Hosting;

/// <summary>
/// Trae desde el almacén gestionado los valores de configuración que son secretos.
/// </summary>
/// <remarks>
/// Hoy es uno solo: el secreto de cliente de Google. No puede llegar por variable de
/// entorno porque entonces estaría en el plan de infraestructura y en el flujo, que es
/// justo lo que el despliegue evita; y no puede leerse con el puerto ISecretStore porque
/// el proveedor de autenticación se configura al arrancar, antes de que exista ningún
/// servicio del que pedirlo.
/// </remarks>
public static class ManagedSecrets
{
    /// <summary>
    /// Sólo se cargan los secretos con este prefijo.
    /// </summary>
    /// <remarks>
    /// En el mismo almacén viven las credenciales de los brókeres, que son muchas y
    /// cambian a diario. Traerlas todas a la configuración las dejaría cargadas en
    /// memoria sin que nadie las pida y cacheadas hasta el siguiente reinicio, cuando el
    /// camino bueno para ellas es el puerto que las lee una a una cuando hacen falta.
    /// </remarks>
    private const string Prefix = "Authentication--";

    public static void AddManagedSecrets(this IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration["KeyVault:Uri"] is not { Length: > 0 } vaultUri)
        {
            return;
        }

        configuration.AddAzureKeyVault(
            new Uri(vaultUri),
            new AzureIdentity::Azure.Identity.DefaultAzureCredential(),
            new AzureKeyVaultConfigurationOptions { Manager = new AuthenticationSecrets() });
    }

    /// <summary>Qué secretos se cargan y con qué nombre se ven en la configuración.</summary>
    internal sealed class AuthenticationSecrets : KeyVaultSecretManager
    {
        public override bool Load(SecretProperties secret) =>
            secret is not null && secret.Name.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>
        /// Traduce el nombre del almacén al de la configuración.
        /// </summary>
        /// <remarks>
        /// Un nombre de secreto sólo admite letras, números y guiones, así que la
        /// jerarquía se escribe con dos guiones: Authentication--Google--ClientSecret es
        /// Authentication:Google:ClientSecret.
        /// </remarks>
        public override string GetKey(KeyVaultSecret secret) =>
            secret is null
                ? throw new ArgumentNullException(nameof(secret))
                : secret.Name.Replace("--", ":", StringComparison.Ordinal);
    }
}
