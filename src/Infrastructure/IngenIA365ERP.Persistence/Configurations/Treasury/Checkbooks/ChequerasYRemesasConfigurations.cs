using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Checkbooks;

// Feature 015, US19 (T0225; data-model §11): chequeras, cheques girados y remesas de Tesorería. FK Restrict a la plataforma y entre tablas TES_; ninguna a RCD_ (la bóveda
// de Recaudo va por su PublicId, R-05).

/// <summary><c>TES_Checkbooks</c>. (nuevo)</summary>
public class CheckbookConfiguration : IEntityTypeConfiguration<Checkbook>
{
    public const string Tabla = "TES_Checkbooks";

    public void Configure(EntityTypeBuilder<Checkbook> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CustodianUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.NextCheckNumber).HasColumnName("NextNumber");
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.HasIndex(e => new { e.BankAccountId, e.FromNumber }).HasDatabaseName($"IX_{Tabla}_Account_From");
    }
}

/// <summary><c>TES_IssuedChecks</c>: el número es único por chequera. (nuevo)</summary>
public class IssuedCheckConfiguration : IEntityTypeConfiguration<IssuedCheck>
{
    public const string Tabla = "TES_IssuedChecks";

    public void Configure(EntityTypeBuilder<IssuedCheck> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<Checkbook>().WithMany().HasForeignKey(e => e.CheckbookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(e => e.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentBatchItem>().WithMany().HasForeignKey(e => e.PaymentBatchItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.BeneficiaryPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.HasIndex(e => new { e.CheckbookId, e.CheckNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_Checkbook_Number");
        builder.HasIndex(e => e.PaymentOrderId).HasDatabaseName($"IX_{Tabla}_PaymentOrderId");
        builder.HasIndex(e => e.Status).HasDatabaseName($"IX_{Tabla}_Status");
        // T1 (par TesoreriaCompleta, fase 33; FR-220): el cierre del mes que lo pasó a cuenta por pagar.
        builder.HasOne<MonthEndCheckRun>().WithMany().HasForeignKey(e => e.MovedToPayableRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.MovedToPayableRunId).HasDatabaseName($"IX_{Tabla}_MovedToPayableRunId");
    }
}

/// <summary><c>TES_IssuedCheckEvents</c> (H). (nuevo)</summary>
public class IssuedCheckEventConfiguration : IEntityTypeConfiguration<IssuedCheckEvent>
{
    public const string Tabla = "TES_IssuedCheckEvents";

    public void Configure(EntityTypeBuilder<IssuedCheckEvent> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<IssuedCheck>().WithMany().HasForeignKey(e => e.IssuedCheckId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.FromStatus).HasConversion<int>();
        builder.Property(e => e.ToStatus).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.IssuedCheckId).HasDatabaseName($"IX_{Tabla}_IssuedCheckId");
    }
}

/// <summary><c>TES_CashRemittances</c>: número único; una sola remesa por la clave de quien la registra. (nuevo)</summary>
public class CashRemittanceConfiguration : IEntityTypeConfiguration<CashRemittance>
{
    public const string Tabla = "TES_CashRemittances";

    public void Configure(EntityTypeBuilder<CashRemittance> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LiquidityFundUse>().WithMany().HasForeignKey(e => e.LiquidityFundUseId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Direction).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.BagSeal).HasMaxLength(40);
        builder.Property(e => e.SourceDocumentNumber).HasMaxLength(40);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName($"UK_{Tabla}_IdempotencyKey");
        builder.HasIndex(e => new { e.Status, e.BankAccountId }).HasDatabaseName($"IX_{Tabla}_Status_Account");
    }
}
