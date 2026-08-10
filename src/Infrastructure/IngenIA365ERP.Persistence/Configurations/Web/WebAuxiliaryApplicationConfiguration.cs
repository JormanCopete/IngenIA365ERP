using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Web;

public class WebAuxiliaryApplicationConfiguration : IEntityTypeConfiguration<WebAuxiliaryApplication>
{
    public void Configure(EntityTypeBuilder<WebAuxiliaryApplication> builder)
    {
        builder.ToTable("WEB_AuxiliaryApplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.SubsidyType).HasMaxLength(5);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
