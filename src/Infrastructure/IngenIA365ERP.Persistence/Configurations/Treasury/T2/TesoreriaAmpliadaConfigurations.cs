using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.T2;

// Feature 015, T2 (par TesoreriaAmpliada; data-model §13.2; DT §2.3): las tablas TES_ de T2. FK Restrict a la plataforma y entre tablas TES_; ninguna a RCD_, ACC_, INV_
// ni TRS_ (lo de otro módulo se guarda por su PublicId o como texto). Cada fase de T2 suma las suyas y regenera el par.

/// <summary><c>TES_LegacyPendingLoads</c>: por clase y referencia heredada (única entre las vivas). (nuevo)</summary>
public class LegacyPendingLoadConfiguration : IEntityTypeConfiguration<LegacyPendingLoad>
{
    public const string Tabla = "TES_LegacyPendingLoads";

    public void Configure(EntityTypeBuilder<LegacyPendingLoad> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.LegacyReference).HasMaxLength(60).IsRequired();
        builder.Property(e => e.BeneficiaryName).HasMaxLength(200);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(40);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.Kind, e.LegacyReference }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Kind_Reference");
    }
}

/// <summary><c>TES_ReconciliationRules</c>: código único entre las vivas; por cuenta y prioridad. (nuevo)</summary>
public class ReconciliationRuleConfiguration : IEntityTypeConfiguration<ReconciliationRule>
{
    public const string Tabla = "TES_ReconciliationRules";

    public void Configure(EntityTypeBuilder<ReconciliationRule> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Criterion).HasConversion<int>();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName($"UK_{Tabla}_Code");
        builder.HasIndex(e => new { e.BankAccountId, e.Priority }).HasDatabaseName($"IX_{Tabla}_Account_Priority");
    }
}

/// <summary><c>TES_BankNotes</c>: número único; por cuenta y estado; por la línea del extracto. (nuevo)</summary>
public class BankNoteConfiguration : IEntityTypeConfiguration<BankNote>
{
    public const string Tabla = "TES_BankNotes";

    public void Configure(EntityTypeBuilder<BankNote> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.ThirdPartyPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Direction).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.BaseAmount).Monto();
        builder.Property(e => e.VatAmount).Monto();
        builder.Property(e => e.WithholdingAmount).Monto();
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.BankAccountId, e.Status }).HasDatabaseName($"IX_{Tabla}_Account_Status");
        builder.HasIndex(e => e.StatementLinePublicId).HasDatabaseName($"IX_{Tabla}_StatementLine");
    }
}

/// <summary><c>TES_PayrollDispersions</c>: el archivo de Nómina es único; por cuenta y día de pago. (nuevo)</summary>
public class PayrollDispersionConfiguration : IEntityTypeConfiguration<PayrollDispersion>
{
    public const string Tabla = "TES_PayrollDispersions";

    public void Configure(EntityTypeBuilder<PayrollDispersion> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.SourceBankCode).HasMaxLength(10);
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30);
        builder.Property(e => e.Total).Monto();
        builder.HasIndex(e => e.FilePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_File");
        builder.HasIndex(e => new { e.BankAccountId, e.PayDate }).HasDatabaseName($"IX_{Tabla}_Account_PayDate");
    }
}
