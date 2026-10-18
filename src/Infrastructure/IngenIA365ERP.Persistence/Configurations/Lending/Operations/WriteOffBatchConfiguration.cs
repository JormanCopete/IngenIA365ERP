using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Operations;

// Feature 013, K3 (T0875; data-model §25.6): el lote de castigo y sus ítems (CarteraNovedades; su comportamiento lo construye US14).

/// <summary><c>LND_WriteOffBatches</c>: número único; el punto del acta del consejo que lo aprueba.</summary>
public class WriteOffBatchConfiguration : IEntityTypeConfiguration<WriteOffBatch>
{
    public const string Tabla = "LND_WriteOffBatches";

    public void Configure(EntityTypeBuilder<WriteOffBatch> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CutoffDate).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.PreparedAt).IsRequired();
        builder.Property(e => e.LoanCount).IsRequired();
        builder.Property(e => e.TotalPrincipal).Monto().IsRequired();
        builder.Property(e => e.TotalInterest).Monto().IsRequired();
        builder.Property(e => e.TotalOther).Monto().IsRequired();
        builder.Property(e => e.SupervisorReportRequired).IsRequired();
        builder.Property(e => e.SupervisorReportReference).HasMaxLength(60);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PreparedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SessionItem>().WithMany().HasForeignKey(e => e.SessionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AppliedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_WriteOffBatches_Number");
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_LND_WriteOffBatches_Status");
        builder.HasIndex(e => e.PreparedByUserId).HasDatabaseName("IX_LND_WriteOffBatches_PreparedByUserId");
        builder.HasIndex(e => e.SessionItemId).HasDatabaseName("IX_LND_WriteOffBatches_SessionItemId");
        builder.HasIndex(e => e.AppliedByUserId).HasDatabaseName("IX_LND_WriteOffBatches_AppliedByUserId");
    }
}

/// <summary><c>LND_WriteOffBatchItems</c>: <c>UK (WriteOffBatchId, LoanId)</c> y <c>(LoanId)</c>; la operación <c>WriteOff</c> por navegación.</summary>
public class WriteOffBatchItemConfiguration : IEntityTypeConfiguration<WriteOffBatchItem>
{
    public const string Tabla = "LND_WriteOffBatchItems";

    public void Configure(EntityTypeBuilder<WriteOffBatchItem> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequirementsJson).IsRequired();
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.Property(e => e.PrincipalAmount).Monto().IsRequired();
        builder.Property(e => e.InterestAmount).Monto().IsRequired();
        builder.Property(e => e.OtherAmount).Monto().IsRequired();
        builder.Property(e => e.ImpairmentAtCutoff).Monto().IsRequired();

        builder.HasOne<WriteOffBatch>().WithMany().HasForeignKey(e => e.WriteOffBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WriteOffOperation).WithMany().HasForeignKey(e => e.WriteOffOperationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.WriteOffBatchId, e.LoanId }).IsUnique().HasDatabaseName("UK_LND_WriteOffBatchItems_Batch_Loan");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_WriteOffBatchItems_LoanId");
        builder.HasIndex(e => e.WriteOffOperationId).HasDatabaseName("IX_LND_WriteOffBatchItems_WriteOffOperationId");
    }
}
