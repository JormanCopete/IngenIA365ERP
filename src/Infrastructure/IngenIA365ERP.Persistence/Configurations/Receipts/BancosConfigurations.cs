using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, US6 (T0192; data-model §5): la identificación de los abonos del extracto, las partidas por identificar y las corridas no interactivas. FK Restrict a la
// plataforma y entre tablas RCD_; la línea del extracto y la cuenta propia (de Tesorería) se guardan por su PublicId, sin FK (R-05).

/// <summary><c>RCD_BankItemIdentifications</c> (H): UK <c>(BankStatementLinePublicId, Sequence)</c>. (nuevo)</summary>
public class BankItemIdentificationConfiguration : IEntityTypeConfiguration<BankItemIdentification>
{
    public const string Tabla = "RCD_BankItemIdentifications";

    public void Configure(EntityTypeBuilder<BankItemIdentification> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnidentifiedItem>().WithMany().HasForeignKey(e => e.UnidentifiedItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MiscConcept>().WithMany().HasForeignKey(e => e.MiscConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.IdentifiedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.BankStatementLinePublicId, e.Sequence }).IsUnique().HasDatabaseName($"UK_{Tabla}_Line_Sequence");
        builder.HasIndex(e => e.ReceiptId).HasDatabaseName($"IX_{Tabla}_ReceiptId");
        builder.HasIndex(e => e.UnidentifiedItemId).HasDatabaseName($"IX_{Tabla}_UnidentifiedItemId");
    }
}

/// <summary><c>RCD_UnidentifiedItems</c>: número único; por estado y antigüedad. (nuevo)</summary>
public class UnidentifiedItemConfiguration : IEntityTypeConfiguration<UnidentifiedItem>
{
    public const string Tabla = "RCD_UnidentifiedItems";

    public void Configure(EntityTypeBuilder<UnidentifiedItem> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Bank>().WithMany().HasForeignKey(e => e.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Balance).Monto();
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.DepositorName).HasMaxLength(200);
        builder.Property(e => e.DepositorTaxId).HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.OriginKind).HasConversion<int>();
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.Status, e.OpenedOn }).HasDatabaseName($"IX_{Tabla}_Status_OpenedOn");
        builder.HasIndex(e => e.BankStatementLinePublicId).HasDatabaseName($"IX_{Tabla}_BankStatementLinePublicId");
        builder.HasIndex(e => e.DepositorTaxId).HasDatabaseName($"IX_{Tabla}_DepositorTaxId");
    }
}

/// <summary><c>RCD_UnidentifiedItemResolutions</c> (H). (nuevo)</summary>
public class UnidentifiedItemResolutionConfiguration : IEntityTypeConfiguration<UnidentifiedItemResolution>
{
    public const string Tabla = "RCD_UnidentifiedItemResolutions";

    public void Configure(EntityTypeBuilder<UnidentifiedItemResolution> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<UnidentifiedItem>().WithMany().HasForeignKey(e => e.UnidentifiedItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MiscConcept>().WithMany().HasForeignKey(e => e.MiscConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.DocumentNumber).HasMaxLength(30);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.UnidentifiedItemId).HasDatabaseName($"IX_{Tabla}_UnidentifiedItemId");
        builder.HasIndex(e => e.DocumentPublicId).HasDatabaseName($"IX_{Tabla}_DocumentPublicId");
    }
}

/// <summary><c>RCD_ProcessingRuns</c>. (nuevo)</summary>
public class ProcessingRunConfiguration : IEntityTypeConfiguration<ProcessingRun>
{
    public const string Tabla = "RCD_ProcessingRuns";

    public void Configure(EntityTypeBuilder<ProcessingRun> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.StartedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.SourceNumber).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Total).Monto();
        builder.Property(e => e.InputsHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => new { e.Kind, e.SourcePublicId }).HasDatabaseName($"IX_{Tabla}_Kind_SourcePublicId");
    }
}

/// <summary><c>RCD_ProcessingRunLines</c>: UK <c>(ProcessingRunId, IdempotencyKey)</c>. (nuevo)</summary>
public class ProcessingRunLineConfiguration : IEntityTypeConfiguration<ProcessingRunLine>
{
    public const string Tabla = "RCD_ProcessingRunLines";

    public void Configure(EntityTypeBuilder<ProcessingRunLine> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<ProcessingRun>().WithMany().HasForeignKey(e => e.ProcessingRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.LineRef).HasMaxLength(60).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Code).HasMaxLength(100);
        builder.Property(e => e.Message).HasMaxLength(1000);
        builder.Property(e => e.RequestJson).HasMaxLength(8000).IsRequired();
        builder.HasIndex(e => new { e.ProcessingRunId, e.IdempotencyKey }).IsUnique().HasDatabaseName($"UK_{Tabla}_Run_Key");
        builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName($"IX_{Tabla}_IdempotencyKey");
    }
}
