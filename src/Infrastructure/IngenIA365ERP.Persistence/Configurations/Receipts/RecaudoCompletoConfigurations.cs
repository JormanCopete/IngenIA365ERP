using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, R1 (par RecaudoCompleto; data-model §7.3, §8.1; DT §2.3): las tablas RCD_ de R1. FK Restrict a la plataforma y entre tablas RCD_; nada hacia LND_ ni
// TES_ (lo que viene de otro módulo se guarda por su PublicId). Cada fase de R1 suma las suyas y regenera el par.

/// <summary><c>RCD_PendingApplications</c> (H): el recaudo pendiente de aplicar, uno por aplicación del recibo (FR-046). (nuevo)</summary>
public class PendingApplicationConfiguration : IEntityTypeConfiguration<PendingApplication>
{
    public const string Tabla = "RCD_PendingApplications";

    public void Configure(EntityTypeBuilder<PendingApplication> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ReceiptApplication>().WithMany().HasForeignKey(e => e.ReceiptApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.TargetModule).HasConversion<int>();
        builder.Property(e => e.ProductReference).HasMaxLength(40);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.ReceiptApplicationId).IsUnique().HasDatabaseName($"UK_{Tabla}_ReceiptApplicationId");
        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName($"IX_{Tabla}_PersonId_Status");
    }
}

/// <summary><c>RCD_PendingApplicationResolutions</c> (H): la aplicación del pendiente por el contrato del módulo ya activado; una por pendiente. (nuevo)</summary>
public class PendingApplicationResolutionConfiguration : IEntityTypeConfiguration<PendingApplicationResolution>
{
    public const string Tabla = "RCD_PendingApplicationResolutions";

    public void Configure(EntityTypeBuilder<PendingApplicationResolution> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<PendingApplication>().WithMany().HasForeignKey(e => e.PendingApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.ProductNumber).HasMaxLength(40);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ModuleOperationNumber).HasMaxLength(40);
        builder.HasIndex(e => e.PendingApplicationId).IsUnique().HasDatabaseName($"UK_{Tabla}_PendingApplicationId");
    }
}
