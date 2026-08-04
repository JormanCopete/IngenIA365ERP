using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Web;

public class WebServiceConfiguration : IEntityTypeConfiguration<WebService>
{
    public void Configure(EntityTypeBuilder<WebService> builder)
    {
        builder.ToTable("WEB_Services");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.ServiceNumber, e.ServiceType, e.IdentificationNumber }).IsUnique();

        builder.Property(e => e.ServiceType).HasMaxLength(2);
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20);
        builder.Property(e => e.ReviewedBy).HasMaxLength(50);
        builder.Property(e => e.Status).HasMaxLength(2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
