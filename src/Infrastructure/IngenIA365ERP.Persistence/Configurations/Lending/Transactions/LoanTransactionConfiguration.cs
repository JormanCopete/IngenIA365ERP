using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Transactions;

/// <summary>
/// <c>LND_LoanTransactions</c> (feature 013; data-model §5.1): el libro, hecho <c>bigint</c>. Exactamente un origen (operación o
/// corrida), importe distinto de cero, una causación por crédito, concepto, cuota y día (dos únicos porque los nulos se comparan
/// distinto) y un espejo por fila.
/// </summary>
public class LoanTransactionConfiguration : IEntityTypeConfiguration<LoanTransaction>
{
    public const string Tabla = "LND_LoanTransactions";

    public void Configure(EntityTypeBuilder<LoanTransaction> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_LoanTransactions_Origin",
                "([OperationId] IS NOT NULL AND [ProcessRunId] IS NULL) OR ([OperationId] IS NULL AND [ProcessRunId] IS NOT NULL)");
            t.HasCheckConstraint("CK_LND_LoanTransactions_Amount", "[Amount] <> 0");
        });

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Concept).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.ValueDate).IsRequired();
        builder.Property(e => e.AccountingDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.InControl).IsRequired();
        // K6b (T1059; CarteraVivienda): el valor en UVR de la fila y la UVR con que se valoró.
        builder.Property(e => e.UvrAmount).ValorUvr();
        builder.Property(e => e.UvrValue).ValorUvr();

        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanTransaction>().WithMany().HasForeignKey(e => e.ReversesTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.Concept, e.InstallmentNumber, e.ValueDate }).IsUnique()
            .HasDatabaseName("UK_LND_LoanTransactions_Accrual")
            .HasFilter("[Kind] = 3 AND [ReversesTransactionId] IS NULL AND [InstallmentNumber] IS NOT NULL");
        builder.HasIndex(e => new { e.LoanId, e.Concept, e.ValueDate }).IsUnique()
            .HasDatabaseName("UK_LND_LoanTransactions_AccrualNoInst")
            .HasFilter("[Kind] = 3 AND [ReversesTransactionId] IS NULL AND [InstallmentNumber] IS NULL");
        builder.HasIndex(e => e.ReversesTransactionId).IsUnique()
            .HasDatabaseName("UK_LND_LoanTransactions_Reverses").HasFilter("[ReversesTransactionId] IS NOT NULL");

        builder.HasIndex(e => new { e.LoanId, e.ValueDate, e.Id }).HasDatabaseName("IX_LND_LoanTransactions_Loan_ValueDate");
        builder.HasIndex(e => new { e.LoanId, e.AccountingDate }).HasDatabaseName("IX_LND_LoanTransactions_Loan_AccountingDate");
        builder.HasIndex(e => e.AccountingDate).HasDatabaseName("IX_LND_LoanTransactions_AccountingDate");
        builder.HasIndex(e => e.OperationId)
            .HasDatabaseName("IX_LND_LoanTransactions_OperationId").HasFilter("[OperationId] IS NOT NULL");
        builder.HasIndex(e => e.ProcessRunId)
            .HasDatabaseName("IX_LND_LoanTransactions_ProcessRunId").HasFilter("[ProcessRunId] IS NOT NULL");
        builder.HasIndex(e => e.MessagePublicId)
            .HasDatabaseName("IX_LND_LoanTransactions_MessagePublicId").HasFilter("[MessagePublicId] IS NOT NULL");
    }
}
