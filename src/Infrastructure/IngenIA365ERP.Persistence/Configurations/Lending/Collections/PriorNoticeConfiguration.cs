using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Collections;

/// <summary><c>LND_PriorNotices</c> (feature 013, C.3; data-model §26.11): hecho. Un aviso por crédito, titular y episodio de mora.</summary>
public class PriorNoticeConfiguration : IEntityTypeConfiguration<PriorNotice>
{
    public const string Tabla = "LND_PriorNotices";

    public void Configure(EntityTypeBuilder<PriorNotice> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.EpisodeStartDate).IsRequired();
        builder.Property(e => e.RequiredCommunications).IsRequired();
        builder.Property(e => e.MinimumDays).IsRequired();
        builder.Property(e => e.ThresholdAmount).Monto();
        builder.Property(e => e.MigratedEvidenceReference).HasMaxLength(200);
        builder.Property(e => e.MigratedBureauCode).HasMaxLength(20);

        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.PersonId, e.EpisodeStartDate }).IsUnique()
            .HasDatabaseName("UK_LND_PriorNotices_Loan_Person_Episode");
    }
}
