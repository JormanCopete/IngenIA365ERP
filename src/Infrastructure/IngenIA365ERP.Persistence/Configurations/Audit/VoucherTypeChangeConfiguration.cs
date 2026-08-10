using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Audit;

public class VoucherTypeChangeConfiguration : IEntityTypeConfiguration<VoucherTypeChange>
{
    public void Configure(EntityTypeBuilder<VoucherTypeChange> builder)
    {
        builder.ToTable("AUD_VoucherTypeChanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Action).HasMaxLength(10).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(100);
        builder.Property(e => e.IpAddress).HasMaxLength(50);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
