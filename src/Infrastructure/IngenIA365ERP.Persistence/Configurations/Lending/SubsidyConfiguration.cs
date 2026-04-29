using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class SubsidyConfiguration : IEntityTypeConfiguration<Subsidy>
{
    public void Configure(EntityTypeBuilder<Subsidy> builder)
    {
        builder.ToTable("LND_Subsidies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_Subsidies_PublicId");

        builder.Property(e => e.SubsidyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(25).IsRequired();
        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.TaxRate).HasPrecision(10, 5);
        builder.HasIndex(e => e.SubsidyCode).IsUnique().HasDatabaseName("UK_LND_Subsidies_Code");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
