using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class SavingsAccountConfiguration : IEntityTypeConfiguration<SavingsAccount>
{
    public void Configure(EntityTypeBuilder<SavingsAccount> builder)
    {
        builder.ToTable("LND_SavingsAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_SavingsAccounts_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LegalRepresentative).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RepresentativeName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CommercialAddress).HasMaxLength(25).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CellPhone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.UniqueAccount).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AutoDebit).HasMaxLength(2).IsRequired();
        builder.Property(e => e.DeductionType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.PaymentCycle).HasMaxLength(2).IsRequired();
        builder.Property(e => e.HasSeal).HasMaxLength(2).IsRequired();
        builder.Property(e => e.HasProtector).HasMaxLength(2).IsRequired();
        builder.HasIndex(e => e.AccountNumber).IsUnique().HasDatabaseName("UK_LND_SavingsAccounts_AccountNumber");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
