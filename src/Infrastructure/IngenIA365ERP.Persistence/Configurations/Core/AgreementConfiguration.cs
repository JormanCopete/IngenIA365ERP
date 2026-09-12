using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class AgreementConfiguration : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> builder)
    {
        builder.ToTable("COR_Agreements");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Agreements_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        // Único cuando existe: los registros migrados del SOLIDO pueden venir sin código.
        builder.HasIndex(e => e.LegacyCode).IsUnique().HasFilter("[LegacyCode] IS NOT NULL");
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(60);
        builder.Property(e => e.EntityCode).HasMaxLength(10);
        builder.Property(e => e.Currency).HasDefaultValue((short)0);
        builder.Property(e => e.SavingsCode).HasMaxLength(4);
        builder.Property(e => e.CheckingCode).HasMaxLength(4);
        builder.Property(e => e.BlockCode).HasMaxLength(4);
        builder.Property(e => e.AvailabilityOption).HasDefaultValue((short)0);
        builder.Property(e => e.AvailabilityLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.AvailabilityRate).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.AtmOption).HasDefaultValue((short)0);
        builder.Property(e => e.AtmLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.AtmRate).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.AtmTransactions).HasDefaultValue((short)0);
        builder.Property(e => e.PosOption).HasDefaultValue((short)0);
        builder.Property(e => e.PosLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.PosRate).HasPrecision(10, 2).HasDefaultValue(0m);
        builder.Property(e => e.PosTransactions).HasDefaultValue((short)0);
        builder.Property(e => e.ShowBalances).HasDefaultValue((short)0);
        builder.Property(e => e.Bin).HasDefaultValue(0);
        builder.Property(e => e.AvailableLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.CashLimit).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.OutputPath).HasMaxLength(200);
        builder.Property(e => e.InputPath).HasMaxLength(200);
        builder.Property(e => e.AverageDays).HasDefaultValue(0);
        builder.Property(e => e.FreeTransactions).HasDefaultValue(0);
        builder.Property(e => e.HandlingFee).HasDefaultValue(0);
        builder.Property(e => e.AvailableLimit2).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.CashLimit2).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.ServiceType).HasDefaultValue(0);

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
