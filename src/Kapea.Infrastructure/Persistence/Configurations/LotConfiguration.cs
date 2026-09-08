using Kapea.Domain.Lots;
using Kapea.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("Lots");
        builder.HasKey(lot => lot.Id);

        builder.Property(lot => lot.UserId).IsRequired();

        builder.ComplexProperty(lot => lot.AcquiredAt, acquired =>
        {
            acquired.Property(value => value.Instant).HasColumnName("AcquiredAt").IsRequired();
            acquired.Property(value => value.SourceTimeZoneId).HasColumnName("AcquiredAtTimeZoneId").HasMaxLength(64).IsRequired();
        });

        builder.ComplexProperty(lot => lot.AcquisitionCost, cost =>
        {
            cost.Property(value => value.Amount).HasColumnName("AcquisitionCostAmount")
                .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
            cost.Property(value => value.Currency).HasColumnName("AcquisitionCostCurrency").HasMaxLength(3).IsRequired();
            cost.Ignore(value => value.IsZero);
            cost.Ignore(value => value.IsNegative);
        });

        builder.Ignore(lot => lot.IsExhausted);
        builder.Ignore(lot => lot.UnitCost);
        builder.Ignore(lot => lot.RemainingCost);

        // El orden de consumo se resuelve por fecha y número de secuencia, así que el
        // índice cubre exactamente la consulta que hace el motor al recalcular.
        builder.HasIndex(lot => new { lot.UserId, lot.AssetId });
    }
}
