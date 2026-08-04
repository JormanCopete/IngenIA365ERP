using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Audit;

public class AuditReferenceConfiguration : IEntityTypeConfiguration<AuditReference>
{
    public void Configure(EntityTypeBuilder<AuditReference> builder)
    {
        builder.ToTable("AUD_AuditReferences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(10).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(100);
        builder.Property(e => e.ExternalDocumentId).HasMaxLength(100);
        builder.Property(e => e.Summary).HasMaxLength(500);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
