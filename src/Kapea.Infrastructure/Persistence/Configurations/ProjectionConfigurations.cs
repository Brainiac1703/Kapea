using Kapea.Domain.Calculation;
using Kapea.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Resultado realizado. Su clave es la transmisión que lo origina: una transmisión
/// produce exactamente un resultado, así que no hace falta inventar un identificador.
/// </summary>
internal sealed class RealizedResultConfiguration : IEntityTypeConfiguration<RealizedResult>
{
    public void Configure(EntityTypeBuilder<RealizedResult> builder)
    {
        builder.ToTable("RealizedResults");
        builder.HasKey(result => result.DisposalTransactionId);

        builder.Property(result => result.UserId).IsRequired();

        builder.ComplexProperty(result => result.DisposedAt, disposed =>
        {
            disposed.Property(value => value.Instant).HasColumnName("DisposedAt").IsRequired();
            disposed.Property(value => value.SourceTimeZoneId).HasColumnName("DisposedAtTimeZoneId").HasMaxLength(64).IsRequired();
        });

        Money(builder.ComplexProperty(result => result.ProceedsInEuros), "Proceeds");
        Money(builder.ComplexProperty(result => result.AcquisitionCostInEuros), "AcquisitionCost");

        // El desglose por lote es una entidad propia y no un tipo poseído porque
        // necesita mapear importes y fechas como tipos complejos, y el constructor de
        // tipos poseídos no los admite.
        builder.HasMany(result => result.ConsumedLots)
            .WithOne()
            .HasForeignKey(ConsumedLotConfiguration.ForeignKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(result => result.ResultInEuros);
        builder.Ignore(result => result.TaxYear);

        builder.HasIndex(result => new { result.UserId, result.AssetId });
    }

    internal static void Money(ComplexPropertyBuilder<Domain.ValueObjects.Money> builder, string prefix)
    {
        builder.Property(value => value.Amount).HasColumnName(prefix + "Amount")
            .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
        builder.Property(value => value.Currency).HasColumnName(prefix + "Currency").HasMaxLength(3).IsRequired();
        builder.Ignore(value => value.IsZero);
        builder.Ignore(value => value.IsNegative);
    }
}

internal sealed class ConsumedLotConfiguration : IEntityTypeConfiguration<ConsumedLot>
{
    internal const string ForeignKey = "DisposalTransactionId";

    public void Configure(EntityTypeBuilder<ConsumedLot> builder)
    {
        builder.ToTable("RealizedResultLots");
        builder.Property<Guid>(ForeignKey);
        builder.HasKey(ForeignKey, nameof(ConsumedLot.LotId));

        builder.ComplexProperty(consumed => consumed.AcquiredAt, acquired =>
        {
            acquired.Property(value => value.Instant).HasColumnName("AcquiredAt").IsRequired();
            acquired.Property(value => value.SourceTimeZoneId).HasColumnName("AcquiredAtTimeZoneId").HasMaxLength(64).IsRequired();
        });

        RealizedResultConfiguration.Money(builder.ComplexProperty(consumed => consumed.AcquisitionCostInEuros), "AcquisitionCost");
        RealizedResultConfiguration.Money(builder.ComplexProperty(consumed => consumed.ProceedsInEuros), "Proceeds");

        builder.Ignore(consumed => consumed.ResultInEuros);
    }
}

internal sealed class CapitalIncomeConfiguration : IEntityTypeConfiguration<CapitalIncome>
{
    public void Configure(EntityTypeBuilder<CapitalIncome> builder)
    {
        builder.ToTable("CapitalIncomes");
        builder.HasKey(income => income.TransactionId);

        builder.Property(income => income.UserId).IsRequired();

        builder.Property(income => income.Type).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.ComplexProperty(income => income.ReceivedAt, received =>
        {
            received.Property(value => value.Instant).HasColumnName("ReceivedAt").IsRequired();
            received.Property(value => value.SourceTimeZoneId).HasColumnName("ReceivedAtTimeZoneId").HasMaxLength(64).IsRequired();
        });

        RealizedResultConfiguration.Money(builder.ComplexProperty(income => income.GrossAmountInEuros), "Gross");
        RealizedResultConfiguration.Money(builder.ComplexProperty(income => income.WithholdingInEuros), "Withholding");

        builder.Ignore(income => income.NetAmountInEuros);
        builder.Ignore(income => income.TaxYear);

        builder.HasIndex(income => new { income.UserId, income.AssetId });
    }
}

/// <summary>
/// Sistemas de especulación y sus versiones.
/// </summary>
/// <remarks>
/// Las reglas se guardan como JSON en una columna. Son un árbol y se consultan enteras,
/// nunca por partes, así que desmenuzarlas en tablas solo añadiría uniones para volver a
/// juntarlas al leer.
/// </remarks>
internal sealed class StrategyConfiguration : IEntityTypeConfiguration<Domain.Strategies.Strategy>
{
    public void Configure(EntityTypeBuilder<Domain.Strategies.Strategy> builder)
    {
        builder.ToTable("Strategies");
        builder.HasKey(strategy => strategy.Id);

        builder.Property(strategy => strategy.UserId).IsRequired();
        builder.Property(strategy => strategy.Name).HasMaxLength(120).IsRequired();
        builder.Property(strategy => strategy.Description).HasMaxLength(2000);

        builder.Ignore(strategy => strategy.CurrentVersion);
        builder.Ignore(strategy => strategy.Current);

        builder.OwnsMany(strategy => strategy.Versions, version =>
        {
            version.ToTable("StrategyVersions");
            version.WithOwner().HasForeignKey("StrategyId");
            version.HasKey(entity => entity.Id);

            version.Property(entity => entity.Number).IsRequired();
            version.Property(entity => entity.CreatedAt).IsRequired();
            version.Property(entity => entity.Entry).HasConversion<ConditionConverter>().IsRequired();
            version.Property(entity => entity.Exit).HasConversion<ConditionConverter>();
            version.Property(entity => entity.Target).HasConversion<LevelConverter>();
            version.Property(entity => entity.StopLoss).HasConversion<LevelConverter>();

            version.Ignore(entity => entity.RequiredDays);

            version.HasIndex("StrategyId", nameof(Domain.Strategies.StrategyVersion.Number)).IsUnique();
        });

        builder.Navigation(strategy => strategy.Versions).AutoInclude();
        builder.HasIndex(strategy => new { strategy.UserId, strategy.Name }).IsUnique();
    }
}

/// <summary>Fuentes externas que se siguen. Del contenido ajeno solo se guarda el enlace.</summary>
internal sealed class IdeaSourceConfiguration : IEntityTypeConfiguration<Domain.Ideas.IdeaSource>
{
    public void Configure(EntityTypeBuilder<Domain.Ideas.IdeaSource> builder)
    {
        builder.ToTable("IdeaSources");
        builder.HasKey(source => source.Id);

        builder.Property(source => source.UserId).IsRequired();
        builder.Property(source => source.Name).HasMaxLength(120).IsRequired();
        builder.Property(source => source.Channel).HasMaxLength(200);
        builder.Property(source => source.LastSeenUrl).HasMaxLength(500);

        builder.HasIndex(source => new { source.UserId, source.Name }).IsUnique();
    }
}

/// <summary>
/// Ideas que llegan de fuera.
/// </summary>
/// <remarks>
/// El símbolo se guarda tal como lo nombró la fuente, además del activo resuelto: si
/// mañana el catálogo cambia de nombre un activo, la idea sigue diciendo de qué hablaba
/// la publicación.
/// </remarks>
internal sealed class ExternalIdeaConfiguration : IEntityTypeConfiguration<Domain.Ideas.ExternalIdea>
{
    public void Configure(EntityTypeBuilder<Domain.Ideas.ExternalIdea> builder)
    {
        builder.ToTable("ExternalIdeas");
        builder.HasKey(idea => idea.Id);

        builder.Property(idea => idea.UserId).IsRequired();
        builder.Property(idea => idea.Symbol).HasMaxLength(32).IsRequired();
        builder.Property(idea => idea.Direction).HasConversion<string>().HasMaxLength(8).IsRequired();
        builder.Property(idea => idea.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(idea => idea.Url).HasMaxLength(500);
        builder.Property(idea => idea.Note).HasMaxLength(2000);

        Amount(builder.ComplexProperty(idea => idea.Entry), "Entry", required: false);
        Amount(builder.ComplexProperty(idea => idea.Target), "Target", required: false);
        Amount(builder.ComplexProperty(idea => idea.StopLoss), "Stop", required: false);

        builder.Ignore(idea => idea.CanBeTracked);

        builder.HasIndex(idea => new { idea.UserId, idea.PublishedOn });
        builder.HasIndex(idea => idea.SourceId);
    }

    /// <summary>Un importe en dos columnas, con su divisa.</summary>
    private static void Amount(
        Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder<Domain.ValueObjects.Money> builder,
        string prefix,
        bool required)
    {
        builder.IsRequired(required);
        builder.Property(value => value.Amount).HasColumnName(prefix + "Amount")
            .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
        builder.Property(value => value.Currency).HasColumnName(prefix + "Currency").HasMaxLength(3);
        builder.Ignore(value => value.IsZero);
        builder.Ignore(value => value.IsNegative);
    }
}

/// <summary>
/// Anotaciones del diario.
/// </summary>
/// <remarks>
/// Sin clave foránea al movimiento a propósito: borrar una importación no puede llevarse
/// por delante lo que el usuario escribió, que es lo único de la fila que no se puede
/// volver a descargar.
/// </remarks>
internal sealed class DecisionNoteConfiguration : IEntityTypeConfiguration<Domain.Journal.DecisionNote>
{
    public void Configure(EntityTypeBuilder<Domain.Journal.DecisionNote> builder)
    {
        builder.ToTable("DecisionNotes");
        builder.HasKey(note => note.Id);

        builder.Property(note => note.UserId).IsRequired();
        builder.Property(note => note.Text).HasMaxLength(4000).IsRequired();
        builder.Property(note => note.WrittenAt).IsRequired();

        builder.HasIndex(note => new { note.UserId, note.WrittenAt });
        builder.HasIndex(note => note.TransactionId);
        builder.HasIndex(note => note.SignalId);
    }
}

/// <summary>Señales emitidas. La huella impide guardar dos veces la misma.</summary>
internal sealed class EmittedSignalConfiguration : IEntityTypeConfiguration<Domain.Strategies.EmittedSignal>
{
    public void Configure(EntityTypeBuilder<Domain.Strategies.EmittedSignal> builder)
    {
        builder.ToTable("EmittedSignals");
        builder.HasKey(signal => signal.Id);

        builder.Property(signal => signal.UserId).IsRequired();
        builder.Property(signal => signal.Direction).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(signal => signal.Reason).HasMaxLength(1000).IsRequired();

        Amount(builder.ComplexProperty(signal => signal.PriceInEuros), "Price", required: true);
        Amount(builder.ComplexProperty(signal => signal.Target), "Target", required: false);
        Amount(builder.ComplexProperty(signal => signal.StopLoss), "Stop", required: false);

        builder.Property(signal => signal.Fingerprint).HasMaxLength(200).IsRequired();
        builder.HasIndex(signal => signal.Fingerprint).IsUnique();
        builder.HasIndex(signal => new { signal.UserId, signal.Date });
    }

    /// <summary>Un importe en dos columnas, con su divisa.</summary>
    private static void Amount(
        Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder<Domain.ValueObjects.Money> builder,
        string prefix,
        bool required)
    {
        builder.IsRequired(required);
        builder.Property(value => value.Amount).HasColumnName(prefix + "Amount")
            .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
        builder.Property(value => value.Currency).HasColumnName(prefix + "Currency").HasMaxLength(3);
        builder.Ignore(value => value.IsZero);
        builder.Ignore(value => value.IsNegative);
    }
}

/// <summary>
/// Precios diarios. La clave es activo y fecha, que es lo que impide que el mismo día
/// acabe con dos precios distintos según quién lo trajera.
/// </summary>
internal sealed class DailyPriceConfiguration : IEntityTypeConfiguration<Domain.MarketData.DailyPrice>
{
    public void Configure(EntityTypeBuilder<Domain.MarketData.DailyPrice> builder)
    {
        builder.ToTable("DailyPrices");
        builder.HasKey(price => new { price.AssetId, price.Date });

        builder.Property(price => price.PriceInEuros)
            .HasPrecision(ValueObjectConverters.RatePrecision, ValueObjectConverters.RateScale)
            .IsRequired();

        builder.Property(price => price.Source).HasMaxLength(32).IsRequired();
    }
}

/// <summary>
/// Hasta dónde se pidió la serie de cada activo. Una fila por activo: no interesa el
/// historial de peticiones, sólo el tramo que entre todas ya abarcan.
/// </summary>
internal sealed class PriceHistoryReachConfiguration
    : IEntityTypeConfiguration<Domain.MarketData.PriceHistoryReach>
{
    public void Configure(EntityTypeBuilder<Domain.MarketData.PriceHistoryReach> builder)
    {
        builder.ToTable("PriceHistoryReaches");
        builder.HasKey(reach => reach.AssetId);

        builder.Property(reach => reach.RequestedFrom).IsRequired();
        builder.Property(reach => reach.RequestedTo).IsRequired();
        builder.Property(reach => reach.RequestedWith).HasMaxLength(64);
    }
}

/// <summary>Tipos publicados. La clave es divisa y fecha, que es lo que hace idempotente la reingesta.</summary>
internal sealed class DailyRateConfiguration : IEntityTypeConfiguration<Domain.Exchange.DailyRate>
{
    public void Configure(EntityTypeBuilder<Domain.Exchange.DailyRate> builder)
    {
        builder.ToTable("DailyRates");
        builder.HasKey(rate => new { rate.Currency, rate.Date });

        builder.Property(rate => rate.UnitsPerEuro)
            .HasPrecision(ValueObjectConverters.RatePrecision, ValueObjectConverters.RateScale)
            .IsRequired();
    }
}

/// <summary>
/// Incoherencia detectada al calcular. La clave es el movimiento y la clase de
/// incoherencia: un mismo movimiento no puede fallar dos veces por lo mismo, y así el
/// recálculo la reemplaza en lugar de acumular repetidos.
/// </summary>
internal sealed class CalculationInconsistencyConfiguration : IEntityTypeConfiguration<CalculationInconsistency>
{
    public void Configure(EntityTypeBuilder<CalculationInconsistency> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CalculationInconsistencies");
        builder.HasKey(inconsistency => new { inconsistency.TransactionId, inconsistency.Kind });

        builder.Property(inconsistency => inconsistency.UserId).IsRequired();

        builder.ComplexProperty(inconsistency => inconsistency.OccurredAt, occurred =>
        {
            occurred.Property(value => value.Instant).HasColumnName("OccurredAt").IsRequired();
            occurred.Property(value => value.SourceTimeZoneId).HasColumnName("OccurredAtTimeZoneId").HasMaxLength(64).IsRequired();
        });

        builder.HasIndex(inconsistency => new { inconsistency.UserId, inconsistency.AssetId });
    }
}
