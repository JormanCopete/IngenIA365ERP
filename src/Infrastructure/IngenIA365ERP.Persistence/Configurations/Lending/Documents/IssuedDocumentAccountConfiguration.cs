using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocumentAccounts</c> (feature 014, US3, T087; data-model §1.5.1): hecho; una fila por documento y cuenta, el índice por cuenta que usa la
/// anulación por reverso y el título anterior que reemplaza (K9). Migración <c>CaptacionesAportes</c>.
/// </summary>
public class IssuedDocumentAccountConfiguration : IEntityTypeConfiguration<IssuedDocumentAccount>
{
    public const string Tabla = "LND_IssuedDocumentAccounts";

    public void Configure(EntityTypeBuilder<IssuedDocumentAccount> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.BalanceAtCutoff).Monto();

        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.IssuedDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.LastOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_IssuedDocumentAccounts_LastOperation");
        builder.HasOne<MemberAccountTransaction>().WithMany().HasForeignKey(e => e.LastTransactionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_IssuedDocumentAccounts_LastTransaction");
        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.PreviousIssuedDocumentId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_IssuedDocumentAccounts_PreviousDocument");
        // Feature 014, US13 (T194; data-model §1.5.1): el título de CDAT nombra la versión de condiciones que titula.
        builder.HasOne<MemberAccountTermsVersion>().WithMany().HasForeignKey(e => e.TermsVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_IssuedDocumentAccounts_TermsVersion");
        builder.HasIndex(e => e.TermsVersionId).HasDatabaseName("IX_LND_IssuedDocumentAccounts_TermsVersionId");

        builder.HasIndex(e => new { e.IssuedDocumentId, e.MemberAccountId }).IsUnique()
            .HasDatabaseName("UK_LND_IssuedDocumentAccounts_Document_Account");
        builder.HasIndex(e => e.MemberAccountId).HasDatabaseName("IX_LND_IssuedDocumentAccounts_MemberAccountId");
    }
}
