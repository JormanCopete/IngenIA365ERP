using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Documents;

/// <summary><c>LND_IssuedDocumentLoans</c> (feature 013, C.3; data-model §27.2): hecho. Único <c>(IssuedDocumentId, LoanId)</c> e índice <c>(LoanId)</c>.</summary>
public class IssuedDocumentLoanConfiguration : IEntityTypeConfiguration<IssuedDocumentLoan>
{
    public const string Tabla = "LND_IssuedDocumentLoans";

    public void Configure(EntityTypeBuilder<IssuedDocumentLoan> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.BalanceAtCutoff).Monto();

        builder.HasOne<IssuedLendingDocument>().WithMany().HasForeignKey(e => e.IssuedDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.LastOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanTransaction>().WithMany().HasForeignKey(e => e.LastTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.PayoffOperationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.IssuedDocumentId, e.LoanId }).IsUnique()
            .HasDatabaseName("UK_LND_IssuedDocumentLoans_Document_Loan");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_IssuedDocumentLoans_LoanId");
    }
}
