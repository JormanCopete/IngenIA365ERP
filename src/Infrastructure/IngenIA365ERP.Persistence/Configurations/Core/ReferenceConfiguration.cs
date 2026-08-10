using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class ReferenceConfiguration : IEntityTypeConfiguration<Reference>
{
    public void Configure(EntityTypeBuilder<Reference> builder)
    {
        builder.ToTable("COR_References");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_References_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.ReferenceType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(120);
        builder.Property(e => e.Phone).HasMaxLength(40);
        builder.Property(e => e.ContactName).HasMaxLength(120);
        builder.Property(e => e.ProductType).HasMaxLength(2);
        builder.Property(e => e.Mobile).HasMaxLength(40);
        builder.Property(e => e.RelationshipCode).HasMaxLength(10);

        // FK indexes
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_COR_References_PersonId");
        builder.HasIndex(e => e.CityId).HasDatabaseName("IX_COR_References_CityId");

        // Relationships
        builder.HasOne(e => e.City).WithMany(c => c.References).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict);
        // Person relationship configured from PersonConfiguration

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
