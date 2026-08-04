using IngenIA365ERP.Domain.Entities.CDT;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.CDT;

public class CdtAssociateReferenceConfiguration : IEntityTypeConfiguration<CdtAssociateReference>
{
    public void Configure(EntityTypeBuilder<CdtAssociateReference> builder)
    {
        builder.ToTable("CDT_AssociateReferences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.RelationshipType).HasMaxLength(5).IsRequired();
        builder.Property(e => e.ReferenceName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ReferenceAddress).HasMaxLength(100);
        builder.Property(e => e.ReferencePhone).HasMaxLength(30);
        builder.Property(e => e.ReferenceMobile).HasMaxLength(30);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
