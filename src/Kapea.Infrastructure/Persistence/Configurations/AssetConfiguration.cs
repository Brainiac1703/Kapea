using Kapea.Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(asset => asset.Id);

        builder.Property(asset => asset.CanonicalSymbol).HasMaxLength(32).IsRequired();
        builder.Property(asset => asset.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(asset => asset.Isin).HasMaxLength(12);
        builder.Property(asset => asset.Class).HasConversion<string>().HasMaxLength(16).IsRequired();

        // El catálogo es global: el mismo BTC vale para cualquier usuario, y duplicarlo
        // partiría en dos la cola FIFO del activo.
        builder.HasIndex(asset => new { asset.CanonicalSymbol, asset.Class }).IsUnique();
    }
}

/// <summary>
/// Los activos que un usuario vigila. Va aparte del catálogo, que es global.
/// </summary>
internal sealed class WatchedAssetConfiguration : IEntityTypeConfiguration<WatchedAsset>
{
    public void Configure(EntityTypeBuilder<WatchedAsset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WatchedAssets");

        // La clave es el par: un activo se sigue o no se sigue, no dos veces.
        builder.HasKey(watched => new { watched.UserId, watched.AssetId });

        builder.Property(watched => watched.AddedAt).IsRequired();
    }
}
