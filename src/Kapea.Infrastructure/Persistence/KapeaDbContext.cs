using Kapea.Application.Abstractions;
using Kapea.Domain.Accounts;
using Kapea.Domain.Assets;
using Kapea.Domain.Calculation;
using Kapea.Domain.Exchange;
using Kapea.Domain.Lots;
using Kapea.Domain.Transactions;
using Kapea.Domain.ValueObjects;
using Kapea.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;

namespace Kapea.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistencia. Los movimientos son la fuente de verdad; lotes,
/// resultados y rendimientos son proyecciones que se reemplazan enteras por activo
/// en cada recálculo.
/// </summary>
/// <remarks>
/// El aislamiento entre usuarios se apoya en un filtro global y no en que cada
/// consulta se acuerde de filtrar. Una consulta que olvide el filtro explícito sigue
/// sin ver datos ajenos.
/// </remarks>
public sealed class KapeaDbContext(DbContextOptions<KapeaDbContext> options, ICurrentUser currentUser)
    : DbContext(options)
{
    public DbSet<Domain.Identity.User> Users => Set<Domain.Identity.User>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<Platform> Platforms => Set<Platform>();

    public DbSet<Domain.ImportProfiles.ImportProfile> ImportProfiles => Set<Domain.ImportProfiles.ImportProfile>();

    public DbSet<PlatformAccount> Accounts => Set<PlatformAccount>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Lot> Lots => Set<Lot>();

    public DbSet<RealizedResult> RealizedResults => Set<RealizedResult>();

    public DbSet<CapitalIncome> CapitalIncomes => Set<CapitalIncome>();

    public DbSet<Domain.Credentials.BrokerCredential> BrokerCredentials => Set<Domain.Credentials.BrokerCredential>();

    public DbSet<Domain.Import.ImportRun> ImportRuns => Set<Domain.Import.ImportRun>();

    public DbSet<Domain.Transfers.InternalTransfer> InternalTransfers => Set<Domain.Transfers.InternalTransfer>();

    public DbSet<AccountSyncLockRow> AccountSyncLocks => Set<AccountSyncLockRow>();

    public DbSet<DailyRate> DailyRates => Set<DailyRate>();

    public DbSet<Domain.MarketData.DailyPrice> DailyPrices => Set<Domain.MarketData.DailyPrice>();

    public DbSet<Domain.Strategies.Strategy> Strategies => Set<Domain.Strategies.Strategy>();

    public DbSet<Domain.Strategies.EmittedSignal> EmittedSignals => Set<Domain.Strategies.EmittedSignal>();

    public DbSet<Domain.Journal.DecisionNote> DecisionNotes => Set<Domain.Journal.DecisionNote>();

    public DbSet<Domain.Ideas.IdeaSource> IdeaSources => Set<Domain.Ideas.IdeaSource>();

    public DbSet<Domain.Ideas.ExternalIdea> ExternalIdeas => Set<Domain.Ideas.ExternalIdea>();

    internal UserId CurrentUserId => currentUser.Id;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KapeaDbContext).Assembly);

        // Todas las claves las asigna el dominio en sus fábricas, nunca la base de datos.
        // Declararlo importa: con la convención por omisión, EF interpreta que una
        // entidad descubierta por navegación con la clave ya puesta es una que ya existe,
        // y genera un UPDATE de una fila que no está en lugar de insertarla.
        foreach (var key in modelBuilder.Model.GetEntityTypes()
            .Select(entity => entity.FindPrimaryKey())
            .Where(key => key is not null)
            .SelectMany(key => key!.Properties)
            .Where(property => property.ClrType == typeof(Guid)))
        {
            key.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }

        // Filtro global de propiedad. Es una red de seguridad, no la única defensa: aun
        // así toda consulta debería filtrar, pero una que se olvide no devuelve datos
        // ajenos. Assets, DailyRates y DailyPrices quedan fuera a propósito: son catálogos
        // globales sin información de nadie.
        modelBuilder.Entity<PlatformAccount>().HasQueryFilter(account => account.UserId == CurrentUserId);
        // Dos filtros con nombre en los movimientos, para poder quitar uno sin el otro. El
        // de vigentes deja fuera los anulados en toda lectura que alimente cifras sin que
        // cada consulta tenga que acordarse; sólo la deduplicación y la lista de
        // movimientos lo quitan, y nunca el de usuario.
        modelBuilder.Entity<Transaction>()
            .HasQueryFilter(TransactionQueryFilters.Owner, transaction => transaction.UserId == CurrentUserId)
            .HasQueryFilter(TransactionQueryFilters.InForce, transaction => transaction.VoidedAt == null);
        modelBuilder.Entity<Lot>().HasQueryFilter(lot => lot.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Strategies.Strategy>()
            .HasQueryFilter(strategy => strategy.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Strategies.EmittedSignal>()
            .HasQueryFilter(signal => signal.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Journal.DecisionNote>()
            .HasQueryFilter(note => note.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Ideas.IdeaSource>()
            .HasQueryFilter(source => source.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Ideas.ExternalIdea>()
            .HasQueryFilter(idea => idea.UserId == CurrentUserId);
        modelBuilder.Entity<RealizedResult>().HasQueryFilter(result => result.UserId == CurrentUserId);
        modelBuilder.Entity<CapitalIncome>().HasQueryFilter(income => income.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Credentials.BrokerCredential>().HasQueryFilter(credential => credential.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Import.ImportRun>().HasQueryFilter(run => run.UserId == CurrentUserId);
        modelBuilder.Entity<Domain.Transfers.InternalTransfer>().HasQueryFilter(transfer => transfer.UserId == CurrentUserId);

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Currency>().HaveConversion(typeof(ValueObjectConverters.CurrencyConverter)).HaveMaxLength(3);
        configurationBuilder.Properties<Quantity>()
            .HaveConversion(typeof(ValueObjectConverters.QuantityConverter))
            .HavePrecision(ValueObjectConverters.QuantityPrecision, ValueObjectConverters.QuantityScale);
        configurationBuilder.Properties<UserId>().HaveConversion(typeof(ValueObjectConverters.UserIdConverter));
        configurationBuilder.Properties<DateOnly>().HaveConversion(typeof(ValueObjectConverters.DateOnlyConverter));
        configurationBuilder.Properties<decimal>()
            .HavePrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);

        base.ConfigureConventions(configurationBuilder);
    }
}
