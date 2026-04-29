using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class AdvisorConfiguration : IEntityTypeConfiguration<Advisor>
{
    public void Configure(EntityTypeBuilder<Advisor> builder)
    {
        builder.ToTable("COR_Advisors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Advisors_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(20);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(120);
        builder.Property(e => e.Phone).HasMaxLength(40);
        builder.Property(e => e.City).HasMaxLength(20);
        builder.Property(e => e.Mobile).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(120);

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
