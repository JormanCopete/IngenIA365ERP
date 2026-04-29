using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class AssociateCategoryConfiguration : IEntityTypeConfiguration<AssociateCategory>
{
    public void Configure(EntityTypeBuilder<AssociateCategory> builder)
    {
        builder.ToTable("COR_AssociateCategories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_AssociateCategories_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.Category1).HasMaxLength(2);
        builder.Property(e => e.Category2).HasMaxLength(2);
        builder.Property(e => e.Category3).HasMaxLength(2);
        builder.Property(e => e.Category4).HasMaxLength(2);
        builder.Property(e => e.Category5).HasMaxLength(2);
        builder.Property(e => e.DaysCategory).HasDefaultValue((short)0);

        // Unique: one category record per person
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_COR_AssociateCategories_PersonId");

        // FK index
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_COR_AssociateCategories_PersonId");

        // Relationship configured from PersonConfiguration (Cascade)

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
