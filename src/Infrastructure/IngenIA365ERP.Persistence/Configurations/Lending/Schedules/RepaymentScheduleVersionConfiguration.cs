using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Schedules;

/// <summary>
/// <c>LND_RepaymentScheduleVersions</c> (feature 013; data-model §4.4): hecho; único <c>(LoanId, VersionNumber)</c> sin filtro; la
/// operación que la regeneró es <c>bigint</c>.
/// </summary>
public class RepaymentScheduleVersionConfiguration : IEntityTypeConfiguration<RepaymentScheduleVersion>
{
    public const string Tabla = "LND_RepaymentScheduleVersions";

    public void Configure(EntityTypeBuilder<RepaymentScheduleVersion> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.EffectiveValueDate).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.PrincipalBase).Monto().IsRequired();
        builder.Property(e => e.FirstRegeneratedInstallment).IsRequired();
        builder.Property(e => e.InstallmentCount).IsRequired();
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TermsVersion).WithMany().HasForeignKey(e => e.TermsVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.VersionNumber }).IsUnique()
            .HasDatabaseName("UK_LND_RepaymentScheduleVersions_Loan_Version");
    }
}
