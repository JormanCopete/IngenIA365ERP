using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Projections;

/// <summary>
/// <c>LND_LoanDailyPositions</c> (feature 013; data-model §6.1): instantánea versionada <c>bigint</c>; único <c>(LoanId, PositionDate,
/// Version)</c> sin filtro e índice de listados por fecha, oficina y línea.
/// </summary>
public class LoanDailyPositionConfiguration : IEntityTypeConfiguration<LoanDailyPosition>
{
    public const string Tabla = "LND_LoanDailyPositions";

    public void Configure(EntityTypeBuilder<LoanDailyPosition> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.PositionDate).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.RegisteredAt).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ModalityCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SubsegmentCode).HasMaxLength(30);

        builder.Property(e => e.Principal).Monto().IsRequired();
        builder.Property(e => e.PrincipalPastDue).Monto().IsRequired();
        builder.Property(e => e.CurrentInterest).Monto().IsRequired();
        builder.Property(e => e.DefaultInterest).Monto().IsRequired();
        builder.Property(e => e.CurrentInterestInSuspense).Monto().IsRequired();
        builder.Property(e => e.DefaultInterestInSuspense).Monto().IsRequired();
        builder.Property(e => e.Insurance).Monto().IsRequired();
        builder.Property(e => e.CollectionFee).Monto().IsRequired();
        builder.Property(e => e.OtherCharge).Monto().IsRequired();
        builder.Property(e => e.Advance).Monto().IsRequired();
        builder.Property(e => e.CreditBalance).Monto().IsRequired();
        builder.Property(e => e.PayoffAmount).Monto().IsRequired();

        builder.Property(e => e.DaysPastDue).IsRequired();
        builder.Property(e => e.InstallmentsPastDue).IsRequired();
        builder.Property(e => e.AppliedAnnualRate).TasaAnual();
        builder.Property(e => e.DailyRating).HasConversion<int?>();
        builder.Property(e => e.DailyRatingSubcategory).HasConversion<int?>();
        builder.Property(e => e.InputsSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();

        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.LoanId, e.PositionDate, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_LoanDailyPositions_Loan_Date_Version");
        builder.HasIndex(e => new { e.PositionDate, e.BranchId, e.CreditLineId })
            .HasDatabaseName("IX_LND_LoanDailyPositions_Date_Branch_Line");
    }
}
