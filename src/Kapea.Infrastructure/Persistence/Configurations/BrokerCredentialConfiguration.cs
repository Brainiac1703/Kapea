using Kapea.Domain.Credentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class BrokerCredentialConfiguration : IEntityTypeConfiguration<BrokerCredential>
{
    public void Configure(EntityTypeBuilder<BrokerCredential> builder)
    {
        builder.ToTable("BrokerCredentials");
        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.UserId).IsRequired();
        builder.Property(credential => credential.Alias).HasMaxLength(120).IsRequired();

        // Solo el nombre del secreto. La tabla no tiene ninguna columna donde quepa el
        // secreto, así que filtrarlo por aquí exigiría un cambio de esquema deliberado.
        builder.Property(credential => credential.SecretName).HasMaxLength(200).IsRequired();

        builder.Property(credential => credential.Platform).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(credential => credential.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(credential => credential.Scopes).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(credential => credential.InvalidReason).HasMaxLength(500);

        builder.Ignore(credential => credential.IsUsable);

        builder.HasIndex(credential => new { credential.UserId, credential.AccountId });
    }
}
