extern alias AzureIdentity;

using Azure.Security.KeyVault.Secrets;
using Kapea.Application.Abstractions;
using Kapea.Application.Credentials;
using Kapea.Application.Identity;
using Kapea.Application.Import;
using Kapea.Application.Portfolio;
using Kapea.Application.Synchronization;
using Kapea.Infrastructure.ExchangeRates;
using Kapea.Infrastructure.Import.Bit2Me;
using Kapea.Infrastructure.Import.Kraken;
using Kapea.Infrastructure.MarketData;
using Kapea.Infrastructure.Persistence;
using Kapea.Infrastructure.Persistence.Stores;
using Kapea.Infrastructure.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kapea.Infrastructure;

/// <summary>
/// Raíz de composición de la infraestructura. Es el único sitio donde se resuelven
/// dependencias por nombre; el resto del código las recibe por constructor.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddKapeaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<KapeaDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Kapea"),
                sql => sql.EnableRetryOnFailure()));

        services.TryAddTimeProvider();

        // Usuario ambiental por omisión. La API lo sustituye por el que sale del token;
        // los procesos sin petición lo declaran cuenta a cuenta.
        services.AddScoped<AmbientCurrentUser>();
        services.AddScoped<ICurrentUser>(provider => provider.GetRequiredService<AmbientCurrentUser>());
        services.AddScoped<ICurrentUserScope>(provider => provider.GetRequiredService<AmbientCurrentUser>());

        services.AddScoped<IExchangeRateStore, ExchangeRateStore>();
        services.AddScoped<IExchangeRateProvider, StoredExchangeRateProvider>();
        services.AddScoped<IPortfolioProjectionStore, PortfolioProjectionStore>();
        services.AddScoped<IImportRepository, ImportRepository>();
        services.AddScoped<IAssetCatalog, AssetCatalog>();
        services.AddScoped<ISynchronizationRepository, SynchronizationRepository>();
        services.AddScoped<IAccountSyncLock, AccountSyncLock>();
        services.AddScoped<IPortfolioCalculationRepository, PortfolioCalculationRepository>();
        services.AddScoped<PortfolioCalculationService>();
        services.AddScoped<IPortfolioQueries, PortfolioQueries>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<UserSignInService>();
        services.AddScoped<DevelopmentDataAdoption>();
        services.AddScoped<IInternalTransferRepository, InternalTransferRepository>();
        services.AddScoped<InternalTransferService>();
        services.AddScoped<ImportPipeline>();
        services.AddScoped<BrokerCredentialService>();
        services.AddScoped<SynchronizationService>();

        services.AddSecretStore(configuration);
        services.AddExternalClients(configuration);

        // Los adaptadores se registran como colección y el registro los indexa por
        // plataforma: dar de alta una nueva es añadir una línea aquí.
        // Los ficheros ya no tienen un adaptador por plataforma: hay uno solo que aplica
        // el perfil que reconoce las cabeceras.
        services.AddScoped<IFileImporter, Import.Tabular.FileImporter>();
        services.AddScoped<IFileInspector, Import.Tabular.FileInspector>();
        services.AddScoped<IApiImportAdapter, KrakenImportAdapter>();
        services.AddScoped<IApiImportAdapter, Bit2MeImportAdapter>();
        services.AddScoped<IImportAdapterRegistry, ImportAdapterRegistry>();

        services.AddMappingProposals(configuration);

        services.AddScoped<ICredentialVerifier, KrakenCredentialVerifier>();
        services.AddScoped<ICredentialVerifier, Bit2MeCredentialVerifier>();

        return services;
    }

    private static void AddSecretStore(this IServiceCollection services, IConfiguration configuration)
    {
        var vaultUri = configuration["KeyVault:Uri"];

        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            // Sin Key Vault configurado se asume desarrollo local. Es deliberado que
            // haga falta configurarlo para producción y no al revés: un despliegue sin
            // configurar debe fallar por falta de vault, no guardar secretos en claro.
            var secretsId = configuration["UserSecrets:Id"] ?? "kapea-local";
            var store = new UserSecretsSecretStore(UserSecretsSecretStore.DefaultPathFor(secretsId));

            // Se comprueba al componer y no al guardar la primera credencial: un almacén
            // de solo lectura hace fallar el alta con una ruta denegada, mucho después y
            // sin decir qué hay que arreglar.
            store.EnsureWritable();

            services.AddSingleton<ISecretStore>(store);

            return;
        }

        services.AddSingleton(new SecretClient(new Uri(vaultUri), new AzureIdentity::Azure.Identity.DefaultAzureCredential()));
        services.AddSingleton<ISecretStore, KeyVaultSecretStore>();
    }

    private static void AddExternalClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<EcbExchangeRateSource>(client =>
            client.BaseAddress = new Uri(configuration["Ecb:BaseAddress"] ?? "https://www.ecb.europa.eu/"))
            .AddStandardResilienceHandler();

        services.AddScoped<IExchangeRateSource>(provider => provider.GetRequiredService<EcbExchangeRateSource>());

        services.AddHttpClient<KrakenApiClient>(client =>
            client.BaseAddress = new Uri(configuration["Kraken:BaseAddress"] ?? "https://api.kraken.com/"));

        services.AddHttpClient<Bit2MeApiClient>(client =>
            client.BaseAddress = new Uri(configuration["Bit2Me:BaseAddress"] ?? "https://gateway.bit2me.com/"));

        services.AddHttpClient<CoinGeckoMarketPriceProvider>(client =>
            client.BaseAddress = new Uri(configuration["CoinGecko:BaseAddress"] ?? "https://api.coingecko.com/"))
            .AddStandardResilienceHandler();

        services.AddScoped<IMarketPriceProvider>(
            provider => provider.GetRequiredService<CoinGeckoMarketPriceProvider>());
    }

    /// <summary>
    /// Registra el servicio que propone mapeos, o su ausencia.
    /// </summary>
    /// <remarks>
    /// Sin endpoint configurado se registra el que nunca propone nada, en lugar de no
    /// registrar ninguno. Así el resto del código no tiene que preguntar si existe antes
    /// de cada llamada, y la aplicación arranca igual sin el servicio.
    /// </remarks>
    private static void AddMappingProposals(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Import.Mapping.AzureOpenAiOptions>(
            configuration.GetSection(Import.Mapping.AzureOpenAiOptions.SectionName));

        var options = configuration
            .GetSection(Import.Mapping.AzureOpenAiOptions.SectionName)
            .Get<Import.Mapping.AzureOpenAiOptions>() ?? new Import.Mapping.AzureOpenAiOptions();

        if (!options.IsConfigured)
        {
            services.AddScoped<IMappingProposer, Import.Mapping.UnavailableMappingProposer>();

            return;
        }

        services.AddHttpClient<IMappingProposer, Import.Mapping.AzureOpenAiMappingProposer>(client =>
        {
            client.BaseAddress = new Uri(options.Endpoint!.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
