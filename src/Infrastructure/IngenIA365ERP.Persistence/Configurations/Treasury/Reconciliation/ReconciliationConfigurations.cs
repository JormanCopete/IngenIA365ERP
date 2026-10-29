using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Reconciliation;

// Feature 015, US25 (T0276, T0281; data-model §12.2–§12.4): la conciliación bancaria mínima de Tesorería, en el par nuevo y aditivo TesoreriaConciliacionMinima. FK Restrict
// a TES_BankAccounts, TES_BankStatementLines y SEC_Users; ninguna a RCD_ ni a ACC_ (Recaudo y Contabilidad entran por sus puertos, R-05, E-04).

/// <summary><c>TES_Reconciliations</c>: UK <c>(BankAccountId, PeriodYear, PeriodMonth, Version)</c>. (nuevo)</summary>
public class ReconciliationConfiguration : IEntityTypeConfiguration<Domain.Entities.Treasury.Reconciliation.Reconciliation>
{
    public const string Tabla = "TES_Reconciliations";

    public void Configure(EntityTypeBuilder<Domain.Entities.Treasury.Reconciliation.Reconciliation> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ConciliatorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Treasury.Reconciliation.Reconciliation>().WithMany().HasForeignKey(e => e.PreviousReconciliationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.StatementBalance).Monto();
        builder.Property(e => e.LedgerBalance).Monto();
        builder.Property(e => e.ReconcilingItemsTotal).Monto();
        builder.Property(e => e.Difference).Monto();
        builder.Property(e => e.ReopenReason).HasMaxLength(500);
        builder.Property(e => e.OutdatedReason).HasMaxLength(500);
        builder.HasIndex(e => new { e.BankAccountId, e.PeriodYear, e.PeriodMonth, e.Version }).IsUnique()
            .HasDatabaseName($"UK_{Tabla}_Account_Period_Version");
        builder.HasIndex(e => new { e.PeriodYear, e.PeriodMonth, e.Status }).HasDatabaseName($"IX_{Tabla}_PeriodYear_PeriodMonth_Status");
    }
}

/// <summary>
/// <c>TES_ReconciliationMatches</c> (H): un registro y una línea se cruzan a lo sumo una vez en una conciliación (UK filtrados a lo vivo; la línea admite varias filas del
/// mismo grupo, por eso su UK lleva el registro). (nuevo)
/// </summary>
public class ReconciliationMatchConfiguration : IEntityTypeConfiguration<ReconciliationMatch>
{
    public const string Tabla = "TES_ReconciliationMatches";

    public void Configure(EntityTypeBuilder<ReconciliationMatch> builder)
    {
        builder.ComoEntidadLargaDeTesoreria(Tabla);
        builder.HasOne<Domain.Entities.Treasury.Reconciliation.Reconciliation>().WithMany().HasForeignKey(e => e.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankStatementLine>().WithMany().HasForeignKey(e => e.StatementLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.MatchedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UndoneByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.RecordKind).HasConversion<int>();
        builder.Property(e => e.Mode).HasConversion<int>();
        builder.Property(e => e.RecordNumber).HasMaxLength(60);
        builder.Property(e => e.Rule).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.HasIndex(e => new { e.ReconciliationId, e.RecordKind, e.RecordPublicId }).IsUnique().HasFilter("[IsLive] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName($"UK_{Tabla}_Reconciliation_Record_Live");
        builder.HasIndex(e => new { e.ReconciliationId, e.StatementLineId }).HasDatabaseName($"IX_{Tabla}_ReconciliationId_StatementLineId");
    }
}

/// <summary><c>TES_ReconcilingItems</c>: las partidas conciliatorias de una conciliación. (nuevo)</summary>
public class ReconcilingItemConfiguration : IEntityTypeConfiguration<ReconcilingItem>
{
    public const string Tabla = "TES_ReconcilingItems";

    public void Configure(EntityTypeBuilder<ReconcilingItem> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<Domain.Entities.Treasury.Reconciliation.Reconciliation>().WithMany().HasForeignKey(e => e.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankStatementLine>().WithMany().HasForeignKey(e => e.StatementLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.ReconciliationId).HasDatabaseName($"IX_{Tabla}_ReconciliationId");
    }
}
