using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class BeneficiaryConfiguration : IEntityTypeConfiguration<Beneficiary>
{
    public void Configure(EntityTypeBuilder<Beneficiary> builder)
    {
        builder.ToTable("COR_Beneficiaries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Beneficiaries_PublicId");

        builder.Property(e => e.PersonId).IsRequired();
        builder.Property(e => e.BeneficiaryIdNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BeneficiaryName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.DocumentType).HasMaxLength(4);
        builder.Property(e => e.EducationLevel).HasMaxLength(2);
        builder.Property(e => e.HasDisability).HasDefaultValue(false);
        builder.Property(e => e.IsEmployed).HasDefaultValue(false);
        builder.Property(e => e.Percentage).HasPrecision(6, 3).HasDefaultValue(0m);
        builder.Property(e => e.Gender).HasMaxLength(2);
        builder.Property(e => e.Phone).HasMaxLength(20);
        builder.Property(e => e.Address).HasMaxLength(120);
        builder.Property(e => e.BeneficiaryType).HasMaxLength(2).IsRequired().HasDefaultValue("A");
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.LegacyBenefCode).HasMaxLength(20);
        builder.Property(e => e.LegacyNewId).HasMaxLength(20);

        // FK indexes
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_COR_Beneficiaries_PersonId");
        builder.HasIndex(e => e.RelationshipId).HasDatabaseName("IX_COR_Beneficiaries_RelationshipId");
        builder.HasIndex(e => e.CityId).HasDatabaseName("IX_COR_Beneficiaries_CityId");

        // Relationships
        builder.HasOne(e => e.Relationship).WithMany(r => r.Beneficiaries).HasForeignKey(e => e.RelationshipId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.City).WithMany(c => c.Beneficiaries).HasForeignKey(e => e.CityId).OnDelete(DeleteBehavior.Restrict);
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
