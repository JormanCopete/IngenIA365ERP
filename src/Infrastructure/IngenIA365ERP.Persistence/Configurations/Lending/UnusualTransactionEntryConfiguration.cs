using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class UnusualTransactionEntryConfiguration : IEntityTypeConfiguration<UnusualTransactionEntry>
{
    public void Configure(EntityTypeBuilder<UnusualTransactionEntry> builder)
    {
        builder.ToTable("LND_UnusualTransactionEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_UnusualTransactionEntries_PublicId");

        builder.Property(e => e.CurrentStatus).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PriorStatus).HasMaxLength(2);
        builder.Property(e => e.Remarks).HasMaxLength(250).IsRequired();
        builder.Property(e => e.RegisteredBy).HasMaxLength(20).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
