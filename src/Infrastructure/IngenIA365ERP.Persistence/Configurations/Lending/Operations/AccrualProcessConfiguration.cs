using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

// 2026-11-16 (pedido del dueño; feature 024, fase 3): el proceso de causación, sus hechos y sus líneas (migración ProcesoDeCausacion, aditiva).

/// <summary><c>LND_AccrualProcesses</c>: número único; los filtros, el reproceso y quién arma, pide, aprueba y anula, con FK <c>Restrict</c>.</summary>
public class AccrualProcessConfiguration : IEntityTypeConfiguration<AccrualProcess>
{
    public const string Tabla = "LND_AccrualProcesses";

    public void Configure(EntityTypeBuilder<AccrualProcess> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.Concepts).HasConversion<int>().IsRequired();
        builder.Property(e => e.DateFrom).IsRequired();
        builder.Property(e => e.DateTo).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.IssuesJson).IsRequired();
        builder.Property(e => e.IncludedAmount).Monto().IsRequired();
        builder.Property(e => e.ExcludedAmount).Monto().IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000);
        builder.Property(e => e.ApprovedByName).HasMaxLength(200);
        builder.Property(e => e.ApprovalMethod).HasConversion<int?>();
        builder.Property(e => e.VoidReason).HasMaxLength(1000);

        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccrualProcess>().WithMany().HasForeignKey(e => e.ReprocessOfId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccrualProcess>().WithMany().HasForeignKey(e => e.ReprocessedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CalculatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_AccrualProcesses_Number");
        builder.HasIndex(e => new { e.Status, e.DateFrom, e.DateTo }).HasDatabaseName("IX_LND_AccrualProcesses_Status_Range");
        builder.HasIndex(e => e.CreditLineId).HasDatabaseName("IX_LND_AccrualProcesses_CreditLineId");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_LND_AccrualProcesses_BranchId");
        builder.HasIndex(e => e.PaymasterPersonId).HasDatabaseName("IX_LND_AccrualProcesses_PaymasterPersonId");
        builder.HasIndex(e => e.ReprocessOfId).HasDatabaseName("IX_LND_AccrualProcesses_ReprocessOfId");
        builder.HasIndex(e => e.ReprocessedById).HasDatabaseName("IX_LND_AccrualProcesses_ReprocessedById");
        builder.HasIndex(e => e.CreatedByUserId).HasDatabaseName("IX_LND_AccrualProcesses_CreatedByUserId");
        builder.HasIndex(e => e.CalculatedByUserId).HasDatabaseName("IX_LND_AccrualProcesses_CalculatedByUserId");
        builder.HasIndex(e => e.RequestedByUserId).HasDatabaseName("IX_LND_AccrualProcesses_RequestedByUserId");
        builder.HasIndex(e => e.ApprovedByUserId).HasDatabaseName("IX_LND_AccrualProcesses_ApprovedByUserId");
        builder.HasIndex(e => e.VoidedByUserId).HasDatabaseName("IX_LND_AccrualProcesses_VoidedByUserId");
        builder.HasIndex(e => e.ApprovalRequestPublicId).HasDatabaseName("IX_LND_AccrualProcesses_ApprovalRequestPublicId");
    }
}

/// <summary><c>LND_AccrualProcessFacts</c>: los hechos de cada proceso; por mensaje para saber qué proceso tiene un hecho.</summary>
public class AccrualProcessFactConfiguration : IEntityTypeConfiguration<AccrualProcessFact>
{
    public const string Tabla = "LND_AccrualProcessFacts";

    public void Configure(EntityTypeBuilder<AccrualProcessFact> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.FactType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.CausedDate).IsRequired();
        builder.Property(e => e.AccountingDate).IsRequired();
        builder.Property(e => e.LoanCount).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ExclusionReason).HasConversion<int?>();
        builder.Property(e => e.IsBlocking).IsRequired();
        builder.Property(e => e.Detail).HasMaxLength(500);

        builder.HasOne<AccrualProcess>().WithMany().HasForeignKey(e => e.AccrualProcessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.AccrualProcessId, e.Status }).HasDatabaseName("IX_LND_AccrualProcessFacts_Process_Status");
        builder.HasIndex(e => e.MessagePublicId).HasDatabaseName("IX_LND_AccrualProcessFacts_MessagePublicId").HasFilter("[MessagePublicId] IS NOT NULL");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_LND_AccrualProcessFacts_BranchId");
    }
}

/// <summary><c>LND_AccrualProcessLines</c>: bigint; por proceso y estado, y por crédito.</summary>
public class AccrualProcessLineConfiguration : IEntityTypeConfiguration<AccrualProcessLine>
{
    public const string Tabla = "LND_AccrualProcessLines";

    public void Configure(EntityTypeBuilder<AccrualProcessLine> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.FactType).HasMaxLength(60);
        builder.Property(e => e.Concept).HasConversion<int?>();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.PreviousAmount).Monto();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ExclusionReason).HasConversion<int?>();
        // 2026-11-24 (feature 025, fase 3; migración AjusteDelCorte): el detalle por crédito del recálculo de la carga desde el corte.
        builder.Property(e => e.Detail).HasMaxLength(2000);

        builder.HasOne<AccrualProcess>().WithMany().HasForeignKey(e => e.AccrualProcessId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.AccrualProcessId, e.Status }).HasDatabaseName("IX_LND_AccrualProcessLines_Process_Status");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_AccrualProcessLines_LoanId");
        builder.HasIndex(e => e.BranchId).HasDatabaseName("IX_LND_AccrualProcessLines_BranchId");
        builder.HasIndex(e => e.CreditLineId).HasDatabaseName("IX_LND_AccrualProcessLines_CreditLineId");
    }
}
