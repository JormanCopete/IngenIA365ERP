using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.BankAccounts;

// Feature 015, US10 (T0136; data-model §9.2, §9.4): las cuentas bancarias propias y los usos del fondo de liquidez. FK Restrict sólo a la plataforma
// (COR_Banks, COR_Branches, COR_People, SEC_Users) y entre tablas TES_; nunca a ACC_ (el vínculo contable vive en ACC_OwnBankAccountLinks, sin FK hacia aquí).

/// <summary><c>TES_BankAccounts</c>: UK <c>(BankId, AccountNumber)</c> sin filtro (una cuenta no se registra dos veces) y el código único entre vivas. (nuevo)</summary>
public class OwnBankAccountConfiguration : IEntityTypeConfiguration<OwnBankAccount>
{
    public const string Tabla = "TES_BankAccounts";

    public void Configure(EntityTypeBuilder<OwnBankAccount> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BankThirdPartyPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.AccountType).HasConversion<int>();
        builder.Property(e => e.Purposes).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.LastStatementBalance).Monto();
        builder.Property(e => e.OpeningBalance).Monto();
        builder.HasIndex(e => new { e.BankId, e.AccountNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_BankId_AccountNumber");
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Code");
        builder.HasIndex(e => e.BranchId).HasDatabaseName($"IX_{Tabla}_BranchId");
    }
}

/// <summary><c>TES_LiquidityFundUses</c>: número único y búsqueda por cuenta y estado. (nuevo)</summary>
public class LiquidityFundUseConfiguration : IEntityTypeConfiguration<LiquidityFundUse>
{
    public const string Tabla = "TES_LiquidityFundUses";

    public void Configure(EntityTypeBuilder<LiquidityFundUse> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Cause).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.DecisionReason).HasMaxLength(500);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.BankAccountId, e.Status }).HasDatabaseName($"IX_{Tabla}_BankAccountId_Status");
    }
}
