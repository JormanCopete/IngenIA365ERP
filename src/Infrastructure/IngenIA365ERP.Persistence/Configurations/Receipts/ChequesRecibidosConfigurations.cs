using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US15 (T0216; data-model §4.5): los cheques recibidos, sus eventos y el catálogo de causales de devolución. FK Restrict a la plataforma y entre tablas RCD_.

/// <summary><c>RCD_ReceivedChecks</c>: UK <c>(DraweeBankId, AccountNumber, CheckNumber)</c> sin filtro; el medio del recibo lo cita por <c>ReceivedCheckId</c>. (nuevo)</summary>
public class ReceivedCheckConfiguration : IEntityTypeConfiguration<ReceivedCheck>
{
    public const string Tabla = "RCD_ReceivedChecks";

    public void Configure(EntityTypeBuilder<ReceivedCheck> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.DraweeBankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DrawerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PayerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayerReceivable>().WithMany().HasForeignKey(e => e.PayerReceivableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany<ReceiptMeans>().WithOne().HasForeignKey(m => m.ReceivedCheckId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.CheckNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.DrawerName).HasMaxLength(200);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ReturnCauseCode).HasMaxLength(10);
        builder.HasIndex(e => new { e.DraweeBankId, e.AccountNumber, e.CheckNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_Bank_Account_Number");
        // R2 (par RecaudoCanales, fase 37; FR-084): el lote de canje del cheque.
        builder.HasOne<ClearingBatch>().WithMany().HasForeignKey(e => e.ClearingBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ClearingBatchId).HasDatabaseName($"IX_{Tabla}_ClearingBatchId");
        builder.HasIndex(e => new { e.Status, e.BranchId }).HasDatabaseName($"IX_{Tabla}_Status_Branch");
    }
}

/// <summary><c>RCD_ReceivedCheckEvents</c> (H). (nuevo)</summary>
public class ReceivedCheckEventConfiguration : IEntityTypeConfiguration<ReceivedCheckEvent>
{
    public const string Tabla = "RCD_ReceivedCheckEvents";

    public void Configure(EntityTypeBuilder<ReceivedCheckEvent> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<ReceivedCheck>().WithMany().HasForeignKey(e => e.ReceivedCheckId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.FromStatus).HasConversion<int>();
        builder.Property(e => e.ToStatus).HasConversion<int>();
        builder.Property(e => e.CauseCode).HasMaxLength(10);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.ReceivedCheckId).HasDatabaseName($"IX_{Tabla}_ReceivedCheckId");
    }
}

/// <summary><c>RCD_CheckReturnCauses</c>: único por código. (nuevo)</summary>
public class CheckReturnCauseConfiguration : IEntityTypeConfiguration<CheckReturnCause>
{
    public const string Tabla = "RCD_CheckReturnCauses";

    public void Configure(EntityTypeBuilder<CheckReturnCause> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"UK_{Tabla}_Code");
    }
}
