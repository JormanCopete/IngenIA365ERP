using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class DebitAgreementConfiguration : IEntityTypeConfiguration<DebitAgreement>
{
    public void Configure(EntityTypeBuilder<DebitAgreement> builder)
    {
        builder.ToTable("DEB_Agreements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ProcessDate).HasMaxLength(10).IsRequired();
        builder.Property(e => e.ProcessTime).HasMaxLength(10);
        builder.Property(e => e.CardNumber).HasMaxLength(25).IsRequired();
        builder.Property(e => e.AuthCode).HasMaxLength(20);
        builder.Property(e => e.AccountNumber).HasMaxLength(30);
        builder.Property(e => e.NetworkCode).HasMaxLength(2);
        builder.Property(e => e.TransactionType).HasMaxLength(2);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.ConceptCode).HasMaxLength(2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
