using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Payroll;

/// <summary>
/// <c>LND_LoanPayrollLinks</c> (feature 013, C.3; data-model §24.5): un vínculo vigente por crédito y la pagaduría obligatoria sólo
/// en nómina propia y descuento por pagaduría (<c>CK_LND_LoanPayrollLinks_Paymaster</c>). <c>PayrollAgreementId</c> nace con K2
/// (<c>CarteraLibranza</c>).
/// </summary>
public class LoanPayrollLinkConfiguration : IEntityTypeConfiguration<LoanPayrollLink>
{
    public const string Tabla = "LND_LoanPayrollLinks";

    public void Configure(EntityTypeBuilder<LoanPayrollLink> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_LoanPayrollLinks_Paymaster",
            "(([RepaymentChannel] = 2 OR [RepaymentChannel] = 3) AND [PaymasterPersonId] IS NOT NULL) OR " +
            "(([RepaymentChannel] = 1 OR [RepaymentChannel] = 4) AND [PaymasterPersonId] IS NULL)"));

        builder.Property(e => e.RepaymentChannel).HasConversion<int>().IsRequired();
        builder.Property(e => e.IsOwnPayroll).IsRequired();
        builder.Property(e => e.EmployeeIdAtPaymaster).HasMaxLength(30);
        builder.Property(e => e.CertifiedNetIncome).Monto();
        builder.Property(e => e.CertifiedAvailableCapacity).Monto();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.EndReason).HasMaxLength(300);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PaymasterPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanPayrollLink>().WithMany().HasForeignKey(e => e.PreviousLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PayrollAgreement>().WithMany().HasForeignKey(e => e.PayrollAgreementId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.LoanId).IsUnique()
            .HasDatabaseName("UK_LND_LoanPayrollLinks_Loan_Current").HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.PayrollAgreementId)
            .HasDatabaseName("IX_LND_LoanPayrollLinks_PayrollAgreementId").HasFilter("[PayrollAgreementId] IS NOT NULL");
    }
}
