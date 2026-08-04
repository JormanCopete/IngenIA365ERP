using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Accounting;

public class TaxFormCodeConfiguration : IEntityTypeConfiguration<TaxFormCode>
{
    public void Configure(EntityTypeBuilder<TaxFormCode> builder)
    {
        builder.ToTable("ACC_TaxFormCodes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_ACC_TaxFormCodes_PublicId");

        builder.Property(e => e.FormCode).HasMaxLength(10);
        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.AccountCode).HasMaxLength(15);
        builder.Property(e => e.ConceptCode).HasMaxLength(10);
        builder.Property(e => e.TaxType).HasMaxLength(5);
        builder.Property(e => e.EconomicActivity).HasMaxLength(5);
        builder.Property(e => e.ContributorType).HasMaxLength(2);
        builder.Property(e => e.DocumentTypeCode).HasMaxLength(5);
        builder.Property(e => e.RepresentationCode).HasMaxLength(2);
        builder.Property(e => e.AuditorCode).HasMaxLength(1);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
