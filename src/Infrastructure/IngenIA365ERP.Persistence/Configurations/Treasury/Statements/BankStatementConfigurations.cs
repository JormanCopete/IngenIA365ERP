using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Statements;

// Feature 015, US6 (T0191; data-model §12.1): el extracto bancario de Tesorería. FK Restrict a la plataforma y a TES_BankAccounts; ninguna a RCD_ (Recaudo guarda su
// identificación por el PublicId de la línea, R-05).

/// <summary><c>TES_BankStatements</c>: número único; un archivo ya cargado se reconoce por su huella (UK filtrado a <c>FileSha256</c> no nulo). (nuevo)</summary>
public class BankStatementConfiguration : IEntityTypeConfiguration<BankStatement>
{
    public const string Tabla = "TES_BankStatements";

    public void Configure(EntityTypeBuilder<BankStatement> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.BankFileFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Source).HasConversion<int>();
        builder.Property(e => e.FileName).HasMaxLength(200);
        builder.Property(e => e.FileSha256).HasMaxLength(64);
        builder.Property(e => e.OpeningBalance).Monto();
        builder.Property(e => e.ClosingBalance).Monto();
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => new { e.BankAccountId, e.FileSha256 }).IsUnique().HasFilter("[FileSha256] IS NOT NULL")
            .HasDatabaseName($"UK_{Tabla}_BankAccountId_FileSha256");
        builder.HasIndex(e => new { e.BankAccountId, e.To }).HasDatabaseName($"IX_{Tabla}_BankAccountId_To");
    }
}

/// <summary>
/// <c>TES_BankStatementLines</c> (H): UK <c>(BankAccountId, Fingerprint)</c> filtrado a las huellas con identificador del banco (FR-197); búsqueda de los abonos que Recaudo
/// tiene por identificar. (nuevo)
/// </summary>
public class BankStatementLineConfiguration : IEntityTypeConfiguration<BankStatementLine>
{
    public const string Tabla = "TES_BankStatementLines";

    public void Configure(EntityTypeBuilder<BankStatementLine> builder)
    {
        builder.ComoEntidadLargaDeTesoreria(Tabla);
        builder.HasOne<BankStatement>().WithMany().HasForeignKey(e => e.BankStatementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankStatementLine>().WithMany().HasForeignKey(e => e.PossibleDuplicateOfLineId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Direction).HasConversion<int>();
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.BankSequence).HasMaxLength(60);
        builder.Property(e => e.DepositorTaxId).HasMaxLength(20);
        builder.Property(e => e.DepositorName).HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.Property(e => e.Balance).Monto();
        builder.Property(e => e.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DuplicateDecision).HasConversion<int?>();
        builder.Property(e => e.RecaudoStatus).HasConversion<int>();
        builder.Property(e => e.RecaudoIdentifiedAmount).Monto();
        builder.Property(e => e.ReconciliationStatus).HasConversion<int>();
        builder.HasIndex(e => new { e.BankAccountId, e.Fingerprint }, $"UK_{Tabla}_BankAccountId_Fingerprint").IsUnique().HasFilter("[HasBankIdentifier] = 1");
        builder.HasIndex(e => new { e.BankAccountId, e.Fingerprint }, $"IX_{Tabla}_BankAccountId_Fingerprint_All");
        builder.HasIndex(e => e.BankStatementId).HasDatabaseName($"IX_{Tabla}_BankStatementId");
        builder.HasIndex(e => new { e.RecaudoStatus, e.MovementDate }).HasDatabaseName($"IX_{Tabla}_RecaudoStatus_MovementDate");
    }
}
