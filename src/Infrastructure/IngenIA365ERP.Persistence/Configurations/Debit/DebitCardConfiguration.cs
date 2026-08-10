using IngenIA365ERP.Domain.Entities.Debit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Debit;

public class DebitCardConfiguration : IEntityTypeConfiguration<DebitCard>
{
    public void Configure(EntityTypeBuilder<DebitCard> builder)
    {
        builder.ToTable("DEB_Cards");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.BankId, e.CardNumber }).IsUnique();

        builder.Property(e => e.CardNumber).HasMaxLength(25).IsRequired();
        builder.Property(e => e.BinCode).HasMaxLength(10);
        builder.Property(e => e.AccountType).HasMaxLength(5);
        builder.Property(e => e.ErrorCode).HasMaxLength(5);
        builder.Property(e => e.OperationType).HasMaxLength(5);
        builder.Property(e => e.Status).HasMaxLength(2);
        builder.Property(e => e.InitialConcept).HasMaxLength(20);
        builder.Property(e => e.Pin).HasMaxLength(100);
        builder.Property(e => e.IsDebitOrCredit).HasMaxLength(2).HasDefaultValue("D");
        builder.Property(e => e.CoSigner1).HasMaxLength(20);
        builder.Property(e => e.CoSigner2).HasMaxLength(20);
        builder.Property(e => e.TerminalId).HasMaxLength(30);
        builder.Property(e => e.BlockedByUserId).HasMaxLength(20);
        builder.Property(e => e.AvailableBalance).HasPrecision(18, 2);
        builder.Property(e => e.DailyAtmLimit).HasPrecision(18, 2);
        builder.Property(e => e.DailyPosLimit).HasPrecision(18, 2);
        builder.Property(e => e.CreditLimit).HasPrecision(18, 2);
        builder.Property(e => e.DomesticAvailableBalance).HasPrecision(18, 2);
        builder.Property(e => e.DomesticAtmLimit).HasPrecision(18, 2);
        builder.Property(e => e.DomesticPosLimit).HasPrecision(18, 2);
        builder.Property(e => e.LegacyLineId).HasPrecision(18, 0);

        builder.HasMany(e => e.Transactions).WithOne(t => t.Card).HasForeignKey(t => t.CardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
