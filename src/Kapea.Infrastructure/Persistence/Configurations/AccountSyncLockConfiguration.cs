using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class AccountSyncLockConfiguration : IEntityTypeConfiguration<AccountSyncLockRow>
{
    public void Configure(EntityTypeBuilder<AccountSyncLockRow> builder)
    {
        builder.ToTable("AccountSyncLocks");
        builder.HasKey(row => row.AccountId);
    }
}
