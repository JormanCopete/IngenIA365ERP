using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class LoanRestructuringConfiguration : IEntityTypeConfiguration<LoanRestructuring>
{
    public void Configure(EntityTypeBuilder<LoanRestructuring> builder)
    {
        builder.ToTable("LND_LoanRestructurings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_LoanRestructurings_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.OriginalPersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
