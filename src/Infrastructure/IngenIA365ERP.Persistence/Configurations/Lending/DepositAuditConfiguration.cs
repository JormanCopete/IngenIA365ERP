using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class DepositAuditConfiguration : IEntityTypeConfiguration<DepositAudit>
{
    public void Configure(EntityTypeBuilder<DepositAudit> builder)
    {
        builder.ToTable("LND_DepositAudit");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_DepositAudit_PublicId");

        builder.Property(e => e.Action).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20);
        builder.Property(e => e.AuditUserId).HasMaxLength(50);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
