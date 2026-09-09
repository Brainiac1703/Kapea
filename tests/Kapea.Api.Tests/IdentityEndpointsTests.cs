using System.Net;
using System.Net.Http.Json;
using Kapea.Api.Authentication;
using Kapea.Shared.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kapea.Api.Tests;

[Collection(ApiCollection.Name)]
public class IdentityEndpointsTests(KapeaApiFactory factory)
{
    [Fact]
    public async Task Anyone_can_ask_with_which_providers_they_may_enter()
    {
        // Sin sesión: es lo primero que consulta la pantalla de acceso, y quien la abre
        // por definición todavía no ha entrado.
        var providers = await factory.CreateAnonymousClient()
            .GetFromJsonAsync<IReadOnlyList<AuthProviderResponse>>("/auth/providers");

        Assert.NotEmpty(providers!);
        Assert.All(providers!, provider => Assert.False(string.IsNullOrWhiteSpace(provider.DisplayName)));
    }

    [Fact]
    public async Task Entering_through_a_provider_this_installation_does_not_offer_leads_nowhere()
    {
        var response = await factory.CreateAnonymousClient()
            .GetAsync("/auth/signin/Apple");

        // Sin el proveedor registrado no hay a dónde mandar a nadie. Lo que no puede
        // pasar es que devuelva una sesión.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            cookie => cookie.Contains("kapea.session", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_second_provider_registers_and_resolves_exactly_like_the_first()
    {
        // Sirve de comprobación para Apple sin implementarlo: si otro proveedor entra y
        // se reconoce sin tocar el esquema, añadirlo será configuración y no migración.
        var subject = Guid.NewGuid().ToString("N");

        var (_, firstUserId) = await factory.CreateSignedInClientAsync("Apple", subject);
        var (client, secondUserId) = await factory.CreateSignedInClientAsync("Apple", subject);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/me");

        Assert.Equal(firstUserId, secondUserId);
        Assert.Equal("Apple", Assert.Single(me!.Identities).Provider);
    }

    [Fact]
    public async Task The_same_person_reaches_one_account_through_two_providers()
    {
        var (client, userId) = await factory.CreateSignedInClientAsync("Google");

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider
                .GetRequiredService<Application.Identity.UserSignInService>()
                .LinkAsync(
                    new Domain.ValueObjects.UserId(userId),
                    new Application.Identity.ExternalPrincipal(
                        "Apple", Guid.NewGuid().ToString("N"), "Persona de prueba", null));
        }

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/me");

        Assert.Equal(
            ["Apple", "Google"],
            me!.Identities.Select(identity => identity.Provider).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Without_a_session_there_is_no_user()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_signed_in_person_gets_their_own_user()
    {
        var (client, userId) = await factory.CreateSignedInClientAsync();

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/me");

        Assert.Equal(userId, me!.Id);
        Assert.Equal("Google", Assert.Single(me.Identities).Provider);
    }

    [Fact]
    public async Task The_response_carries_nothing_of_the_provider_beyond_its_name()
    {
        // El navegador no necesita el sujeto ni ningún token, y lo que no se envía no
        // se puede filtrar.
        var (client, _) = await factory.CreateSignedInClientAsync(subject: "sujeto-secreto-123");

        var json = await client.GetStringAsync("/api/me");

        Assert.DoesNotContain("sujeto-secreto-123", json, StringComparison.Ordinal);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Two_people_never_see_each_others_data()
    {
        var (mine, _) = await factory.CreateSignedInClientAsync();
        var (theirs, _) = await factory.CreateSignedInClientAsync();

        await theirs.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Kraken", "Cuenta ajena", "EUR"));

        var accounts = await mine.GetFromJsonAsync<List<AccountResponse>>("/api/accounts");

        Assert.DoesNotContain(accounts!, account => account.Alias == "Cuenta ajena");
    }

    [Fact]
    public async Task The_identifier_in_the_request_never_wins_over_the_session()
    {
        var (mine, _) = await factory.CreateSignedInClientAsync();
        var (theirs, theirId) = await factory.CreateSignedInClientAsync();

        await theirs.PostAsJsonAsync("/api/accounts", new CreateAccountRequest("Bit2Me", "Cuenta ajena 2", "EUR"));

        var accounts = await mine.GetFromJsonAsync<List<AccountResponse>>($"/api/accounts?userId={theirId}");

        Assert.DoesNotContain(accounts!, account => account.Alias == "Cuenta ajena 2");
    }

    [Fact]
    public async Task Unlinking_the_only_identity_is_refused()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/me");

        var response = await client.DeleteAsync($"/api/me/identities/{me!.Identities[0].Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("única forma de acceder", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signing_in_again_with_the_same_subject_is_the_same_user()
    {
        var subject = Guid.NewGuid().ToString("N");
        var (_, first) = await factory.CreateSignedInClientAsync(subject: subject);
        var (_, second) = await factory.CreateSignedInClientAsync(subject: subject);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task The_same_subject_from_another_provider_is_another_person()
    {
        // El sujeto solo es único dentro de su proveedor: tomarlo aisladamente
        // confundiría a dos personas distintas.
        var subject = Guid.NewGuid().ToString("N");
        var (_, google) = await factory.CreateSignedInClientAsync("Google", subject);
        var (_, apple) = await factory.CreateSignedInClientAsync("Apple", subject);

        Assert.NotEqual(google, apple);
    }

    [Fact]
    public async Task Signing_out_ends_the_access()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();

        var response = await client.PostAsync("/auth/signout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}

/// <summary>
/// Comprueba de qué se deduce el proveedor al volver del intercambio.
/// </summary>
public class ProviderResolutionTests
{
    [Fact]
    public void The_provider_is_the_one_we_left_with_and_not_the_scheme_that_signed_the_cookie()
    {
        // Al firmar en la cookie el tipo de autenticación pasa a ser el del esquema de
        // sesión. Si se leyera de ahí, la identidad quedaría a nombre de «Kapea» y el
        // siguiente acceso del mismo proveedor no reconocería a nadie.
        var properties = new Microsoft.AspNetCore.Authentication.AuthenticationProperties();
        properties.Items[KapeaAuthentication.ProviderMarker] = "Apple";

        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([], KapeaAuthentication.SessionScheme));

        Assert.Equal("Apple", KapeaAuthentication.ResolveProvider(properties, principal));
    }

    [Fact]
    public void Without_a_marker_the_scheme_that_signed_the_cookie_is_not_taken_for_a_provider()
    {
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([], KapeaAuthentication.SessionScheme));

        Assert.NotEqual(KapeaAuthentication.SessionScheme, KapeaAuthentication.ResolveProvider(null, principal));
    }
}

/// <summary>
/// Comprueba el guardarraíl de arranque sin levantar la API completa: es una regla de
/// composición, y la forma barata de probarla es la que decide si la aplicación
/// arranca o no.
/// </summary>
public class AuthenticationStartupTests
{
    [Fact]
    public void Outside_development_without_a_provider_the_api_refuses_to_start()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddKapeaAuthentication(configuration, isDevelopment: false));

        Assert.Contains("no arranca sin identidad configurada", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void In_development_without_a_provider_it_starts_with_the_development_access()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();

        services.AddKapeaAuthentication(configuration, isDevelopment: true);
    }

    [Fact]
    public void The_development_access_stays_shut_outside_development_however_it_is_configured()
    {
        // Es el guardarraíl que importa: una bandera copiada por descuido a un
        // despliegue dejaría entrar a cualquiera como el usuario de desarrollo.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = "cliente",
                ["Authentication:Google:ClientSecret"] = "secreto",
                ["Authentication:DevelopmentUser:Enabled"] = "true",
            })
            .Build();

        services.AddKapeaAuthentication(configuration, isDevelopment: false);

        var providers = services.BuildServiceProvider().GetRequiredService<AvailableProviders>();

        Assert.False(providers.AllowsDevelopmentSignIn);
        Assert.Equal("Google", Assert.Single(providers.All).Name);
    }

    [Fact]
    public void In_development_the_flag_puts_the_development_access_next_to_the_provider()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = "cliente",
                ["Authentication:DevelopmentUser:Enabled"] = "true",
            })
            .Build();

        services.AddKapeaAuthentication(configuration, isDevelopment: true);

        var providers = services.BuildServiceProvider().GetRequiredService<AvailableProviders>();

        Assert.True(providers.AllowsDevelopmentSignIn);
        Assert.Equal(["Google", "Development"], providers.All.Select(provider => provider.Name));
    }

    [Fact]
    public void In_development_without_the_flag_a_configured_provider_is_the_only_way_in()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = "cliente",
            })
            .Build();

        services.AddKapeaAuthentication(configuration, isDevelopment: true);

        var providers = services.BuildServiceProvider().GetRequiredService<AvailableProviders>();

        Assert.False(providers.AllowsDevelopmentSignIn);
    }

    [Fact]
    public void With_a_provider_configured_it_starts_outside_development()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = "cliente",
                ["Authentication:Google:ClientSecret"] = "secreto",
            })
            .Build();

        services.AddKapeaAuthentication(configuration, isDevelopment: false);
    }
}
