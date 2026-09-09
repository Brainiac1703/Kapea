using Kapea.Domain.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kapea.Infrastructure.Persistence.Configurations;

internal sealed class ImportRunConfiguration : IEntityTypeConfiguration<ImportRun>
{
    public void Configure(EntityTypeBuilder<ImportRun> builder)
    {
        builder.ToTable("ImportRuns");
        builder.HasKey(run => run.Id);

        builder.Property(run => run.UserId).IsRequired();
        builder.Property(run => run.Platform).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(run => run.FileName).HasMaxLength(260);
        builder.Property(run => run.FailureReason).HasMaxLength(1000);

        builder.HasMany(run => run.Records)
            .WithOne()
            .HasForeignKey(record => record.ImportRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Los recuentos se derivan de los registros: persistirlos sería guardar dos
        // veces el mismo dato y arriesgarse a que dejen de coincidir.
        builder.Ignore(run => run.RecordsRead);
        builder.Ignore(run => run.RecordsImported);
        builder.Ignore(run => run.DuplicatesDiscarded);
        builder.Ignore(run => run.RecordsRejected);

        builder.HasIndex(run => new { run.UserId, run.AccountId, run.Status });
    }
}

internal sealed class StagedRecordConfiguration : IEntityTypeConfiguration<StagedRecord>
{
    public void Configure(EntityTypeBuilder<StagedRecord> builder)
    {
        builder.ToTable("ImportStagedRecords");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Outcome).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(record => record.Fingerprint).HasMaxLength(200).IsRequired();
        builder.Property(record => record.NaturalId).HasMaxLength(200);
        builder.Property(record => record.RejectionReason).HasMaxLength(1000);

        // Sin límite: es el registro original íntegro, y recortarlo rompería la
        // trazabilidad justo donde hace falta para defender una cifra.
        builder.Property(record => record.RawContent).IsRequired();
        builder.Property(record => record.Payload).IsRequired();

        builder.HasIndex(record => new { record.ImportRunId, record.Outcome });
    }
}
