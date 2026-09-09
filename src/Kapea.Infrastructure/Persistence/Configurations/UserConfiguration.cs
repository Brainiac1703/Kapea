using Kapea.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(320);

        builder.HasMany(user => user.Identities)
            .WithOne()
            .HasForeignKey(ExternalIdentityConfiguration.ForeignKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    internal const string ForeignKey = "UserId";

    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("ExternalIdentities");
        builder.HasKey(identity => identity.Id);

        // La clave ajena tiene que ser del mismo tipo que la principal, que es UserId y
        // no Guid: es el tipo el que impide pasar por descuento el identificador de otra cosa.
        builder.Property<Domain.ValueObjects.UserId>(ForeignKey);
        builder.Property(identity => identity.Provider).HasMaxLength(32).IsRequired();
        builder.Property(identity => identity.Subject).HasMaxLength(256).IsRequired();

        // Una identidad pertenece a una sola persona. Sin esta restricción, dos usuarios
        // podrían acabar con la misma y el acceso dependería de cuál se leyera primero.
        builder.HasIndex(identity => new { identity.Provider, identity.Subject }).IsUnique();
    }
}
