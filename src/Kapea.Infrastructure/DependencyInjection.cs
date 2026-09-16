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
using Microsoft.Extensions.Logging;

namespace Kapea.Infrastructure;

/// <summary>
/// Raíz de composición de la infraestructura. Es el único sitio donde se resuelven
/// dependencias por nombre; el resto del código las recibe por constructor.
/// </summary>
public static class DependencyInjection
{
    /// <param name="isDevelopment">
    /// Decide qué almacén de secretos se compone. Va como parámetro y no se deduce de la
    /// configuración: el entorno lo sabe el anfitrión, y una variable mal puesta en un
    /// despliegue no debe poder elegir el almacén que guarda en claro.
    /// </param>
    public static IServiceCollection AddKapeaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
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
        services.AddScoped<IPriceHistoryStore, PriceHistoryStore>();
        services.AddScoped<Application.MarketData.IPricedAssetRepository, PricedAssetRepository>();
        services.AddScoped<Application.MarketData.ExchangeRateIngestion>();
        services.AddScoped<Application.MarketData.PriceHistoryUpdater>();
        services.AddScoped<Application.Strategies.IStrategyRepository, StrategyStore>();
        services.AddScoped<Application.Strategies.IStrategyDataSource, StrategyDataSource>();
        services.AddScoped<Application.Strategies.StrategyService>();

        // Sin forma oficial de consultar una fuente, se registra el vigilante que no
        // vigila: así la pantalla puede decirlo y el registro manual sigue funcionando.
        services.AddScoped<Application.Ideas.ISourceWatcher, Application.Ideas.UnavailableSourceWatcher>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<UserSignInService>();
        services.AddScoped<DevelopmentDataAdoption>();
        services.AddScoped<IInternalTransferRepository, InternalTransferRepository>();
        services.AddScoped<InternalTransferService>();
        services.AddScoped<IManualMovementRepository, ManualMovementRepository>();
        services.AddScoped<ManualMovementService>();
        services.AddScoped<ImportPipeline>();
        services.AddScoped<BrokerCredentialService>();
        services.AddScoped<SynchronizationService>();

        services.AddSecretStore(configuration, isDevelopment);
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

        services.AddScoped<IRecordReinterpreter, Bit2MeRecordReinterpreter>();
        services.AddScoped<IReinterpretationRepository, Persistence.Stores.ReinterpretationRepository>();
        services.AddScoped<TransactionReinterpretationService>();

        services.AddMappingProposals(configuration);

        services.AddScoped<ICredentialVerifier, KrakenCredentialVerifier>();
        services.AddScoped<ICredentialVerifier, Bit2MeCredentialVerifier>();

        return services;
    }

    private static void AddSecretStore(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var vaultUri = configuration["KeyVault:Uri"];

        if (string.IsNullOrWhiteSpace(vaultUri))
        {
            // Fuera de desarrollo no hay almacén de reserva: el de desarrollo guarda en
            // claro, y un despliegue al que se le olvidara configurar el almacén
            // gestionado acabaría con las credenciales de los brókeres en un fichero
            // dentro del contenedor. Es mejor que no arranque y lo diga.
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "Falta KeyVault:Uri. Fuera de desarrollo no se compone el almacén de desarrollo, " +
                    "que guarda los secretos en claro.");
            }

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
            CoinGeckoMarketPriceProvider.Configure(
                client,
                new Uri(configuration["CoinGecko:BaseAddress"] ?? "https://api.coingecko.com/")))
            .AddStandardResilienceHandler();

        services.AddHttpClient<YahooMarketPriceProvider>(client =>
            YahooMarketPriceProvider.Configure(
                client,
                new Uri(configuration["Yahoo:BaseAddress"] ?? "https://query1.finance.yahoo.com/")))
            .AddStandardResilienceHandler();

        services.AddMemoryCache();
        services.AddScoped<IAssetClassLookup, AssetClassLookup>();

        // Cada clase de activo con su proveedor: una acción y una criptomoneda no se
        // cotizan en el mismo sitio, y preguntar a los dos por todo gastaría el doble de
        // peticiones para tirar la mitad.
        services.AddScoped(provider => new ClassifiedPriceProvider(
            Domain.Assets.AssetClass.Crypto, provider.GetRequiredService<CoinGeckoMarketPriceProvider>()));

        services.AddScoped(provider => new ClassifiedPriceProvider(
            Domain.Assets.AssetClass.Equity, provider.GetRequiredService<YahooMarketPriceProvider>()));

        services.AddScoped<IMarketPriceProvider, MarketPriceDispatcher>();

        // El histórico va al revés que el precio de ahora: Yahoo primero, porque entrega
        // años de cierres diarios en euros, y CoinGecko después para lo que no cubra,
        // dentro de su ventana gratuita de un año.
        services.AddHttpClient<YahooPriceHistoryProvider>(client =>
            YahooMarketPriceProvider.Configure(
                client,
                new Uri(configuration["Yahoo:BaseAddress"] ?? "https://query1.finance.yahoo.com/")))
            .AddStandardResilienceHandler();

        services.AddHttpClient<CoinGeckoPriceHistoryProvider>(client =>
            CoinGeckoMarketPriceProvider.Configure(
                client,
                new Uri(configuration["CoinGecko:BaseAddress"] ?? "https://api.coingecko.com/")))
            .AddStandardResilienceHandler();

        services.AddScoped<IPriceHistoryProvider>(provider => new PriceHistoryDispatcher(
            [
                provider.GetRequiredService<YahooPriceHistoryProvider>(),
                provider.GetRequiredService<CoinGeckoPriceHistoryProvider>(),
            ],
            provider.GetRequiredService<ILogger<PriceHistoryDispatcher>>()));
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
            services.AddScoped<
                Application.Strategies.IStrategyTranslator,
                Application.Strategies.UnavailableStrategyTranslator>();
            services.AddScoped<Application.Ideas.IIdeaExtractor, Application.Ideas.UnavailableIdeaExtractor>();

            return;
        }

        services.AddHttpClient<IMappingProposer, Import.Mapping.AzureOpenAiMappingProposer>(client =>
            Configure(client, options))
            .AddIdentityIfWithoutKey(options);

        // El mismo servicio, otro trabajo: traducir a reglas lo que se describe con
        // palabras. Comparte configuración porque comparte despliegue.
        services.AddHttpClient<
            Application.Strategies.IStrategyTranslator,
            Strategies.AzureOpenAiStrategyTranslator>(client => Configure(client, options))
            .AddIdentityIfWithoutKey(options);

        services.AddHttpClient<
            Application.Ideas.IIdeaExtractor,
            Ideas.AzureOpenAiIdeaExtractor>(client => Configure(client, options))
            .AddIdentityIfWithoutKey(options);
    }

    private static void Configure(HttpClient client, Import.Mapping.AzureOpenAiOptions options)
    {
        client.BaseAddress = new Uri(options.Endpoint!.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

        if (options.UsesApiKey)
        {
            client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
        }
    }

    /// <summary>
    /// Sin clave configurada, la llamada se autentica con la identidad del proceso.
    /// </summary>
    /// <remarks>
    /// Es lo que permite que la aplicación desplegada no lleve ninguna clave del
    /// servicio de modelos en su configuración, mientras una máquina de desarrollo sigue
    /// usando la clave, que es lo único que tiene.
    /// </remarks>
    private static IHttpClientBuilder AddIdentityIfWithoutKey(
        this IHttpClientBuilder builder,
        Import.Mapping.AzureOpenAiOptions options) =>
        options.UsesApiKey
            ? builder
            : builder.AddHttpMessageHandler(provider => new Http.AzureOpenAiTokenHandler(
                provider.GetService<Azure.Core.TokenCredential>()
                ?? Http.AzureOpenAiTokenHandler.DefaultCredential));

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
