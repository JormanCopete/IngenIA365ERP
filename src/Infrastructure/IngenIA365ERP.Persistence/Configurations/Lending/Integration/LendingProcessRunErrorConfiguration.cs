using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Integration;

/// <summary>
/// <c>LND_ProcessRunErrors</c> (feature 013, C.3; data-model §22.2): único <c>(ProcessRunId, LoanId, AttemptNumber)</c> por crédito
/// y el índice de créditos con error vigente (<c>[ResolvedAt] IS NULL</c>) que lista la vigilancia.
/// </summary>
public class LendingProcessRunErrorConfiguration : IEntityTypeConfiguration<LendingProcessRunError>
{
    public const string Tabla = "LND_ProcessRunErrors";

    public void Configure(EntityTypeBuilder<LendingProcessRunError> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        // Feature 014 (T028; data-model §1.8): un error es de un crédito o de una cuenta, nunca de los dos.
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_ProcessRunErrors_Subject", "[LoanId] IS NULL OR [MemberAccountId] IS NULL"));

        builder.Property(e => e.BatchNumber).IsRequired();
        builder.Property(e => e.AttemptNumber).IsRequired();
        builder.Property(e => e.Step).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ErrorCode).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.DetailJson);
        builder.Property(e => e.OccurredAt).IsRequired();

        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Lending.Deposits.Accounts.MemberAccount>().WithMany()
            .HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProcessRunId, e.LoanId, e.AttemptNumber }).IsUnique()
            .HasDatabaseName("UK_LND_ProcessRunErrors_Run_Loan_Attempt").HasFilter("[LoanId] IS NOT NULL");
        builder.HasIndex(e => e.LoanId)
            .HasDatabaseName("IX_LND_ProcessRunErrors_LoanId_Unresolved").HasFilter("[ResolvedAt] IS NULL");
        builder.HasIndex(e => new { e.ProcessRunId, e.MemberAccountId, e.AttemptNumber }).IsUnique()
            .HasDatabaseName("UK_LND_ProcessRunErrors_Run_Account_Attempt").HasFilter("[MemberAccountId] IS NOT NULL");
    }
}
