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
