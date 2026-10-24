using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, R2 (par RecaudoCanales; data-model §8.2; DT §2.3): las tablas RCD_ de R2. FK Restrict a la plataforma y entre tablas RCD_; nada hacia LND_ ni TES_ (lo
// que viene de otro módulo se guarda por su PublicId). Cada fase de R2 suma las suyas y regenera el par.

/// <summary><c>RCD_ClearingBatches</c>: número único; por cuenta propia y estado. (nuevo)</summary>
public class ClearingBatchConfiguration : IEntityTypeConfiguration<ClearingBatch>
{
    public const string Tabla = "RCD_ClearingBatches";

    public void Configure(EntityTypeBuilder<ClearingBatch> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.TotalAmount).Monto();
        builder.Property(e => e.PaidAmount).Monto();
        builder.Property(e => e.ReturnedAmount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.StatementLinePublicIds).HasMaxLength(1000);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.OwnBankAccountPublicId, e.Status }).HasDatabaseName($"IX_{Tabla}_Account_Status");
    }
}
