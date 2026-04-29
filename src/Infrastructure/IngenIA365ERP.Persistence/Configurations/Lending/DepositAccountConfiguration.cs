using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class DepositAccountConfiguration : IEntityTypeConfiguration<DepositAccount>
{
    public void Configure(EntityTypeBuilder<DepositAccount> builder)
    {
        builder.ToTable("LND_DepositAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_DepositAccounts_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserFullName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(20);
        builder.HasIndex(e => e.AccountNumber).IsUnique().HasDatabaseName("UK_LND_DepositAccounts_AccountNumber");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
