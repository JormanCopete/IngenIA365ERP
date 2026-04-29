using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class UnusualTransactionConfiguration : IEntityTypeConfiguration<UnusualTransaction>
{
    public void Configure(EntityTypeBuilder<UnusualTransaction> builder)
    {
        builder.ToTable("LND_UnusualTransactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_UnusualTransactions_PublicId");

        builder.Property(e => e.ConceptCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CompanyCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Period).HasMaxLength(8).IsRequired();
        builder.Property(e => e.UnusualType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.MonthlyCreditMoves).HasPrecision(18, 2);
        builder.Property(e => e.MaxBalance).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
