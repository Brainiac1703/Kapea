using System.Text.Json;
using Kapea.Domain.Accounts;
using Kapea.Domain.ImportProfiles;
using Kapea.Domain.Transactions;
using Kapea.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Perfiles de importación y sus versiones.
/// </summary>
/// <remarks>
/// Como el catálogo de plataformas, son de la instalación y no de cada usuario:
/// deducir el formato de un extracto una vez debería servir para todos.
/// </remarks>
internal sealed class ImportProfileConfiguration : IEntityTypeConfiguration<ImportProfile>
{
    public void Configure(EntityTypeBuilder<ImportProfile> builder)
    {
        builder.ToTable("ImportProfiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Platform)
            .HasConversion<ValueObjectConverters.PlatformCodeConverter>()
            .HasMaxLength(PlatformCode.MaxLength)
            .IsRequired();

        builder.Property(profile => profile.Name).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.BuiltIn).IsRequired();

        builder.HasIndex(profile => profile.Platform);

        builder.Ignore(profile => profile.Current);

        builder.OwnsMany<ImportProfileVersion>("_versions", ConfigureVersions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(profile => profile.Versions);
    }

    private static void ConfigureVersions(OwnedNavigationBuilder<ImportProfile, ImportProfileVersion> builder)
    {
        builder.ToTable("ImportProfileVersions");
        builder.WithOwner().HasForeignKey("ProfileId");
        builder.HasKey(version => version.Id);

        builder.Property(version => version.Number).IsRequired();
        builder.Property(version => version.CreatedAt).IsRequired();
        builder.Property(version => version.Delimiter).HasMaxLength(1).IsRequired();
        builder.Property(version => version.DecimalConvention).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(version => version.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(version => version.FixedCurrency).HasMaxLength(3);
        builder.Property(version => version.RowShape).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(version => version.AmountSource).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(version => version.FixedAssetClass).HasMaxLength(24);
        builder.Property(version => version.AmountIsAlwaysPositive).IsRequired();

        // Las reglas van como JSON en una columna y no en tablas aparte. Se leen y se
        // escriben siempre enteras, nunca se consultan por dentro, y repartirlas en
        // cuatro tablas solo añadiría uniones para reconstruir lo mismo.
        Json<List<string>>(builder, "_recognizedHeaders", "RecognizedHeaders");
        Json<List<string>>(builder, "_nonFinancialConcepts", "NonFinancialConcepts");
        Json<List<string>>(builder, "_dateFormats", "DateFormats");
        Json<Dictionary<ImportField, string>>(builder, "_columns", "Columns");
        Json<Dictionary<string, TransactionType>>(builder, "_concepts", "Concepts");

        builder.Ignore(version => version.RecognizedHeaders);
        builder.Ignore(version => version.NonFinancialConcepts);
        builder.Ignore(version => version.DateFormats);
        builder.Ignore(version => version.Columns);
        builder.Ignore(version => version.Concepts);

        builder.HasIndex(version => version.Number);
    }

    private static void Json<T>(
        OwnedNavigationBuilder<ImportProfile, ImportProfileVersion> builder,
        string field,
        string column)
        where T : new()
    {
        builder.Property<T>(field)
            .HasColumnName(column)
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                text => JsonSerializer.Deserialize<T>(text, JsonOptions) ?? new T(),
                new ValueComparer<T>(
                    (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
                    value => JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
                    value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions) ?? new T()))
            .IsRequired();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
