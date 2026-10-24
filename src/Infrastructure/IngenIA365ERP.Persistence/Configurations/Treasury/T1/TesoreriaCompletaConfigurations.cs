using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.T1;

// Feature 015, T1 (par TesoreriaCompleta; data-model §13.1; DT §2.3): las tablas TES_ de T1. FK Restrict a la plataforma y entre tablas TES_; ninguna a RCD_ ni a ACC_.

/// <summary><c>TES_Transfers</c>: número único; por cuenta de origen y de destino con su estado. (nuevo)</summary>
public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public const string Tabla = "TES_Transfers";

    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.FromBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.ToBankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LiquidityFundUse>().WithMany().HasForeignKey(e => e.LiquidityFundUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SentByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.DecisionReason).HasMaxLength(500);
        builder.Property(e => e.BankReference).HasMaxLength(60);
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.FromBankAccountId, e.Status }).HasDatabaseName($"IX_{Tabla}_From_Status");
        builder.HasIndex(e => new { e.ToBankAccountId, e.Status }).HasDatabaseName($"IX_{Tabla}_To_Status");
    }
}

/// <summary><c>TES_MonthEndCheckRuns</c> (H): una sola corrida por cuenta y mes. (nuevo)</summary>
public class MonthEndCheckRunConfiguration : IEntityTypeConfiguration<MonthEndCheckRun>
{
    public const string Tabla = "TES_MonthEndCheckRuns";

    public void Configure(EntityTypeBuilder<MonthEndCheckRun> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RunByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ReclassifiedAmount).Monto();
        builder.Property(e => e.MovedToPayableAmount).Monto();
        builder.Property(e => e.DetailJson).IsRequired();
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => e.ReversalPublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_ReversalPublicId");
        builder.HasIndex(e => new { e.BankAccountId, e.PeriodYear, e.PeriodMonth }).IsUnique().HasDatabaseName($"UK_{Tabla}_Account_Period");
    }
}
