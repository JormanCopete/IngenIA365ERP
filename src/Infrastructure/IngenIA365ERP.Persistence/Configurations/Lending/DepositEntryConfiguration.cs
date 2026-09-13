using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class DepositEntryConfiguration : IEntityTypeConfiguration<DepositEntry>
{
    public void Configure(EntityTypeBuilder<DepositEntry> builder)
    {
        builder.ToTable("LND_DepositEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_DepositEntries_PublicId");
        // Indices de lectura (2026-09-13): el extracto de una cuenta de ahorro filtra por
        // AccountNumber y ordena por EntryDate desc (SavingsAccountQueries), y el saldo por
        // lote agrupa por AccountNumber; sin indice ambos recorren la tabla entera de
        // movimientos, la que mas crece en una cooperativa migrada.
        builder.HasIndex(e => new { e.AccountNumber, e.EntryDate }).HasDatabaseName("IX_LND_DepositEntries_Account_Date");
        builder.HasIndex(e => e.PersonCode).HasDatabaseName("IX_LND_DepositEntries_PersonCode");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DeductionType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Periodicity).HasMaxLength(2).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(15).IsRequired();
        builder.Property(e => e.UserFullName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
