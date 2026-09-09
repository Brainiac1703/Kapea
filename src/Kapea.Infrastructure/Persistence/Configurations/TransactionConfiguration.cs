using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.UserId).IsRequired();
        builder.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(transaction => transaction.Origin).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(transaction => transaction.AdjustmentReason).HasMaxLength(500);

        builder.ComplexProperty(transaction => transaction.OccurredAt, occurred =>
        {
            occurred.Property(value => value.Instant).HasColumnName("OccurredAt").IsRequired();
            occurred.Property(value => value.SourceTimeZoneId).HasColumnName("OccurredAtTimeZoneId").HasMaxLength(64).IsRequired();
        });

        MoneyColumns(builder.ComplexProperty(transaction => transaction.GrossAmount), "GrossAmount");
        MoneyColumns(builder.ComplexProperty(transaction => transaction.Fee), "Fee");

        // Precio unitario y retención son opcionales: el origen no siempre los aporta,
        // y un cero fingido en su lugar se confundiría con un dato real.
        builder.ComplexProperty(transaction => transaction.UnitPrice, price =>
        {
            price.Property(value => value.Amount).HasColumnName("UnitPriceAmount")
                .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
            price.Property(value => value.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3);
            price.Ignore(value => value.IsZero);
            price.Ignore(value => value.IsNegative);
        });

        builder.ComplexProperty(transaction => transaction.WithholdingTax, withholding =>
        {
            withholding.Property(value => value.Amount).HasColumnName("WithholdingAmount")
                .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
            withholding.Property(value => value.Currency).HasColumnName("WithholdingCurrency").HasMaxLength(3);
            withholding.Ignore(value => value.IsZero);
            withholding.Ignore(value => value.IsNegative);
        });

        builder.OwnsOne(transaction => transaction.Source, source =>
        {
            source.Property(value => value.ImportRunId).HasColumnName("ImportRunId");
            source.Property(value => value.NaturalId).HasColumnName("SourceNaturalId").HasMaxLength(200);
            source.Property(value => value.RowNumber).HasColumnName("SourceRowNumber");
            source.Property(value => value.Fingerprint).HasColumnName("Fingerprint").HasMaxLength(64).IsRequired();

            // Índice único por cuenta y huella: es lo que hace que reimportar el mismo
            // periodo no duplique, y que el intento quede rechazado por la base de datos
            // y no solo por la comprobación previa en memoria.
            source.HasIndex(value => value.Fingerprint);
        });

        builder.OwnsOne(transaction => transaction.AppliedExchangeRate, rate =>
        {
            rate.Property(value => value.Currency).HasColumnName("RateCurrency").HasMaxLength(3);
            rate.Property(value => value.UnitsPerEuro).HasColumnName("RateUnitsPerEuro")
                .HasPrecision(ValueObjectConverters.RatePrecision, ValueObjectConverters.RateScale);
            rate.Property(value => value.RequestedDate).HasColumnName("RateRequestedDate");
            rate.Property(value => value.RateDate).HasColumnName("RateDate");
            rate.Property(value => value.Source).HasColumnName("RateSource").HasMaxLength(32);
            rate.Ignore(value => value.WasSubstituted);
        });

        builder.Ignore(transaction => transaction.Currency);
        builder.Ignore(transaction => transaction.RequiresReview);
        builder.Ignore(transaction => transaction.IsAcquisition);
        builder.Ignore(transaction => transaction.IsDisposal);
        builder.Ignore(transaction => transaction.GrossAmountInEuros);
        builder.Ignore(transaction => transaction.FeeInEuros);
        builder.Ignore(transaction => transaction.WithholdingTaxInEuros);

        builder.HasIndex(transaction => new { transaction.UserId, transaction.AccountId });
        builder.HasIndex(transaction => new { transaction.UserId, transaction.AssetId });
    }

    private static void MoneyColumns(ComplexPropertyBuilder<Domain.ValueObjects.Money> builder, string prefix)
    {
        builder.Property(value => value.Amount).HasColumnName(prefix + "Amount")
            .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale);
        builder.Property(value => value.Currency).HasColumnName(prefix + "Currency").HasMaxLength(3).IsRequired();
        builder.Ignore(value => value.IsZero);
        builder.Ignore(value => value.IsNegative);
    }
}
