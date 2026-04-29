using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class TransactionCodeConfiguration : IEntityTypeConfiguration<TransactionCode>
{
    public void Configure(EntityTypeBuilder<TransactionCode> builder)
    {
        builder.ToTable("LND_TransactionCodes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_TransactionCodes_PublicId");

        builder.Property(e => e.Code).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(25).IsRequired();
        builder.Property(e => e.TransactionType).HasMaxLength(3).IsRequired();
        builder.Property(e => e.AccountCode).HasMaxLength(15).IsRequired();
        builder.Property(e => e.AdjustAccrual).HasMaxLength(2).IsRequired();
        builder.Property(e => e.DebitCreditFlag).HasMaxLength(2).IsRequired();
        builder.Property(e => e.LegacyCodMovto).HasMaxLength(3);
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_TransactionCodes_Code");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
