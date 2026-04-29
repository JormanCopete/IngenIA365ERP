using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class BiometricRecordConfiguration : IEntityTypeConfiguration<BiometricRecord>
{
    public void Configure(EntityTypeBuilder<BiometricRecord> builder)
    {
        builder.ToTable("LND_BiometricRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_BiometricRecords_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FingerprintString).HasMaxLength(500).IsRequired();
        builder.Property(e => e.FingerprintCode).HasPrecision(10, 0);
        builder.HasIndex(e => e.PersonCode).IsUnique().HasDatabaseName("UK_LND_BiometricRecords_Person");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
