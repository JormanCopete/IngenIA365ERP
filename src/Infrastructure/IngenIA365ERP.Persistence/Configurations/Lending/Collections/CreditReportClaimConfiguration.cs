using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Collections;

/// <summary><c>LND_CreditReportClaims</c> (feature 013, C.3; data-model §26.12): reclamos sobre centrales; índice <c>(PersonId, Status)</c>.</summary>
public class CreditReportClaimConfiguration : IEntityTypeConfiguration<CreditReportClaim>
{
    public const string Tabla = "LND_CreditReportClaims";

    public void Configure(EntityTypeBuilder<CreditReportClaim> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.BureauCode).HasMaxLength(20);
        builder.Property(e => e.ReceivedOn).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.AnswerSummary).HasMaxLength(2000);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DataSubjectRequest>().WithMany().HasForeignKey(e => e.DataSubjectRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.Status }).HasDatabaseName("IX_LND_CreditReportClaims_PersonId_Status");
    }
}
