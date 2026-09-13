using Kapea.Client.Models;
using Kapea.Shared.Contracts;

namespace Kapea.Client.Tests;

/// <summary>
/// Comprueba cuándo un formulario está listo para enviarse y qué dice que falta.
/// </summary>
/// <remarks>
/// Existe por un fallo real: el alta de una credencial se cerraba sin hacer nada y sin
/// decir por qué, porque la cuenta nunca llegaba a asignarse. Un formulario que se
/// descarta en silencio es indistinguible de uno que ha funcionado.
/// </remarks>
public class BrokerCredentialModelTests
{
    [Fact]
    public void A_credential_without_an_account_is_not_ready_and_says_so()
    {
        var model = new BrokerCredentialModel
        {
            Alias = "Kraken",
            ApiKey = "clave",
            ApiSecret = "secreto",
        };

        Assert.False(model.IsComplete);
        Assert.Equal(["Credentials_Account"], model.Missing);
    }

    [Fact]
    public void Everything_that_is_missing_is_listed_at_once()
    {
        // De uno en uno obligaría a intentarlo cuatro veces para enterarse de todo.
        var model = new BrokerCredentialModel();

        Assert.Equal(
            ["Credentials_Account", "Common_Alias", "Credentials_ApiKey", "Credentials_ApiSecret"],
            model.Missing);
    }

    [Fact]
    public void A_complete_credential_has_nothing_missing()
    {
        var model = new BrokerCredentialModel
        {
            AccountId = Guid.NewGuid(),
            Alias = "Kraken",
            ApiKey = "clave",
            ApiSecret = "secreto",
        };

        Assert.True(model.IsComplete);
        Assert.Empty(model.Missing);
    }

    [Fact]
    public void A_rotation_asks_only_for_the_new_keys()
    {
        // Rotar conserva la cuenta y el alias de la credencial que se rota, así que
        // pedirlos otra vez sería pedir algo que ya se sabe.
        var model = new BrokerCredentialModel
        {
            IsRotation = true,
            ApiKey = "clave nueva",
            ApiSecret = "secreto nuevo",
        };

        Assert.True(model.IsComplete);
    }

    [Fact]
    public void A_rotation_without_the_new_keys_is_not_ready()
    {
        var model = new BrokerCredentialModel { IsRotation = true };

        Assert.Equal(["Credentials_ApiKey", "Credentials_ApiSecret"], model.Missing);
    }
}

public class NewAccountModelTests
{
    [Fact]
    public void An_account_without_a_platform_is_not_ready()
    {
        var model = new NewAccountModel { Alias = "Mi cuenta" };

        Assert.False(model.IsComplete);
        Assert.Equal(["Common_Platform"], model.Missing);
    }

    [Fact]
    public void An_account_without_an_alias_is_not_ready()
    {
        var model = new NewAccountModel { Platform = "Xtb" };

        Assert.Equal(["Common_Alias"], model.Missing);
    }

    [Fact]
    public void A_complete_account_has_nothing_missing()
    {
        var model = new NewAccountModel
        {
            AvailablePlatforms = [new PlatformResponse("Xtb", "XTB", "File", true)],
            Platform = "Xtb",
            Alias = "XTB principal",
        };

        Assert.True(model.IsComplete);
    }
}
