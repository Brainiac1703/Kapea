using Kapea.Domain.Accounts;
using Kapea.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

/// <summary>
/// Catálogo de plataformas. Es de la instalación y no de cada usuario: dar de alta un
/// bróker que exporta CSV no debería obligar al siguiente a repetirlo.
/// </summary>
internal sealed class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> builder)
    {
        builder.ToTable("Platforms");

        // La clave es el código y no un identificador generado: ese mismo código viaja
        // dentro de la huella de deduplicación de cada movimiento ya importado.
        builder.HasKey(platform => platform.Code);

        builder.Property(platform => platform.Code)
            .HasConversion<ValueObjectConverters.PlatformCodeConverter>()
            .HasMaxLength(PlatformCode.MaxLength)
            .IsRequired();

        builder.Property(platform => platform.Name).HasMaxLength(120).IsRequired();
        builder.Property(platform => platform.ImportKind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(platform => platform.BuiltIn).IsRequired();

        // Precisión amplia: un 0,95 % se escribe 0,0095 y hay plataformas con cuatro
        // decimales en sus tarifas.
        builder.Property(platform => platform.FeeRate).HasPrecision(9, 6).HasDefaultValue(0m).IsRequired();
        builder.Property(platform => platform.FixedFee)
            .HasPrecision(ValueObjectConverters.MoneyPrecision, ValueObjectConverters.MoneyScale)
            .HasDefaultValue(0m)
            .IsRequired();

        // Las tres que Kapea trae configuradas. Van sembradas y no creadas al arrancar
        // para que existan también en una base creada por migración en un despliegue,
        // donde no hay ningún código de arranque que las ponga.
        builder.HasData(
            new { Code = PlatformCode.Xtb, Name = "XTB", ImportKind = PlatformImportKind.File, BuiltIn = true },
            new { Code = PlatformCode.Kraken, Name = "Kraken", ImportKind = PlatformImportKind.Api, BuiltIn = true },
            new { Code = PlatformCode.Bit2Me, Name = "Bit2Me", ImportKind = PlatformImportKind.Api, BuiltIn = true });
    }
}
