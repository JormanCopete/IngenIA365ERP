using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CollectionCaseConfiguration : IEntityTypeConfiguration<CollectionCase>
{
    public void Configure(EntityTypeBuilder<CollectionCase> builder)
    {
        builder.ToTable("LND_CollectionCases");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CollectionCases_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.DeductionClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.CompanyFrom).HasMaxLength(5).IsRequired();
        builder.Property(e => e.CompanyTo).HasMaxLength(5).IsRequired();
        builder.Property(e => e.LegalCollection).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IsManaged).HasMaxLength(2).IsRequired();
        builder.Property(e => e.TotalOverdueAmount).HasPrecision(16, 3);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
