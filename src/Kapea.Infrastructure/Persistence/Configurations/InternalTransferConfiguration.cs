using Kapea.Domain.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class InternalTransferConfiguration : IEntityTypeConfiguration<InternalTransfer>
{
    public void Configure(EntityTypeBuilder<InternalTransfer> builder)
    {
        builder.ToTable("InternalTransfers");
        builder.HasKey(transfer => transfer.Id);

        builder.Property(transfer => transfer.UserId).IsRequired();
        builder.Property(transfer => transfer.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Ignore(transfer => transfer.NetworkFeeQuantity);
        builder.Ignore(transfer => transfer.IsConfirmed);
        builder.Ignore(transfer => transfer.IsPending);

        // Un emparejamiento se propone una sola vez: la decisión del usuario se conserva
        // para no volver a preguntarle lo mismo en cada sincronización.
        builder.HasIndex(transfer => new { transfer.OutgoingTransactionId, transfer.IncomingTransactionId }).IsUnique();
        builder.HasIndex(transfer => new { transfer.UserId, transfer.Status });
    }
}
