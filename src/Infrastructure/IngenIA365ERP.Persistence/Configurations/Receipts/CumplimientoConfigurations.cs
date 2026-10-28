using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US14 (T0239; data-model §7.1, §7.2): el registro de transacciones en efectivo y las señales evaluadas. FK Restrict a la plataforma.

/// <summary><c>RCD_CashTransactions</c> (H, volumen): índice <c>(PersonId, Month)</c> para el acumulado del mes. (nuevo)</summary>
public class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public const string Tabla = "RCD_CashTransactions";

    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PayerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DeliveredByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.ReceivedByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.DocumentNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.DocumentKind).HasConversion<int>();
        builder.Property(e => e.Direction).HasConversion<int>();
        builder.Property(e => e.Channel).HasConversion<int>();
        builder.Property(e => e.CashAmount).Monto();
        builder.Property(e => e.OtherMeansAmount).Monto();
        builder.Property(e => e.AlertsJson).IsRequired();
        builder.HasIndex(e => new { e.PersonId, e.Month }).HasDatabaseName($"IX_{Tabla}_Person_Month");
        builder.HasIndex(e => new { e.PayerPersonId, e.Month }).HasDatabaseName($"IX_{Tabla}_Payer_Month");
        builder.HasIndex(e => new { e.DocumentPublicId, e.DocumentKind }).IsUnique().HasDatabaseName($"UK_{Tabla}_Document_Kind");
    }
}

/// <summary><c>RCD_ComplianceAlertsLog</c>. (nuevo)</summary>
public class ComplianceAlertLogConfiguration : IEntityTypeConfiguration<ComplianceAlertLog>
{
    public const string Tabla = "RCD_ComplianceAlertsLog";

    public void Configure(EntityTypeBuilder<ComplianceAlertLog> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.DepositorKey).HasMaxLength(40);
        builder.Property(e => e.Signal).HasConversion<int>();
        builder.Property(e => e.Total).Monto();
        builder.Property(e => e.ParameterVersion).HasMaxLength(60);
        builder.HasIndex(e => new { e.PersonId, e.Signal, e.WindowTo }).HasDatabaseName($"IX_{Tabla}_Person_Signal");
    }
}

/// <summary>
/// <c>RCD_ScreeningReleases</c> (H) (segunda ronda del 2026-10-27, D-e; FR-230): una persona liberada de una coincidencia vinculante en una operación de caja; única por
/// operación y persona. (nuevo)
/// </summary>
public class ScreeningReleaseConfiguration : IEntityTypeConfiguration<ScreeningRelease>
{
    public const string Tabla = "RCD_ScreeningReleases";

    public void Configure(EntityTypeBuilder<ScreeningRelease> builder)
    {
        builder.ComoEntidadLargaDeRecaudo(Tabla);
        builder.HasOne<IngenIA365ERP.Domain.Entities.Receipts.Operations.CashOperation>().WithMany().HasForeignKey(e => e.CashOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IngenIA365ERP.Domain.Entities.Security.User>().WithMany().HasForeignKey(e => e.ReleasedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(e => new { e.CashOperationId, e.PersonId }).IsUnique().HasDatabaseName($"UK_{Tabla}_CashOperationId_PersonId");
    }
}
