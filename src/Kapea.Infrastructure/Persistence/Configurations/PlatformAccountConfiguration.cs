using Kapea.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class PlatformAccountConfiguration : IEntityTypeConfiguration<PlatformAccount>
{
    public void Configure(EntityTypeBuilder<PlatformAccount> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(account => account.Id);

        builder.Property(account => account.UserId).IsRequired();
        builder.Property(account => account.Alias).HasMaxLength(120).IsRequired();
        builder.Property(account => account.Platform).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(account => account.BaseCurrency).IsRequired();

        builder.HasIndex(account => account.UserId);
    }
}
