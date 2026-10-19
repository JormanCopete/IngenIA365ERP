using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Documents;

/// <summary>
/// <c>LND_RegulatoryFiles</c> (feature 013, K5, US16; data-model §28.1; migración <c>CarteraInformes</c>): una versión por clase, central y período
/// —<c>UK (PeriodYear, PeriodMonth, Version)</c> filtrado al SICSES y <c>UK (BureauCode, PeriodYear, PeriodMonth, Version)</c> a las centrales—; la
/// central va sólo en las centrales (CHECK). El cierre mensual con FK; el adjunto es dato sin FK, como en los demás archivos de Cartera.
/// </summary>
public class RegulatoryFileConfiguration : IEntityTypeConfiguration<RegulatoryFile>
{
    public const string Tabla = "LND_RegulatoryFiles";

    public void Configure(EntityTypeBuilder<RegulatoryFile> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        // Feature 014, US17 (T220): los formatos de captaciones (3 SICSES de captaciones, 4 exógena) llevan su código de formato y no central.
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_RegulatoryFiles_BureauCode",
            "([Kind] = 1 AND [BureauCode] IS NULL AND [FormatCode] IS NULL) OR ([Kind] = 2 AND [BureauCode] IS NOT NULL AND [FormatCode] IS NULL) "
            + "OR (([Kind] = 3 OR [Kind] = 4) AND [BureauCode] IS NULL AND [FormatCode] IS NOT NULL)"));

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.BureauCode).HasMaxLength(RegulatoryFile.LargoDeLaCentral);
        builder.Property(e => e.FormatCode).HasMaxLength(RegulatoryFile.LargoDelFormato);
        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.LayoutCode).HasMaxLength(RegulatoryFile.LargoDelLayout).IsRequired();
        builder.Property(e => e.LayoutHasUnverifiedFields).IsRequired();
        builder.Property(e => e.ObligatoryModelUnavailable).IsRequired();
        builder.Property(e => e.IssuesJson).IsRequired();
        builder.Property(e => e.BlockingIssueCount).IsRequired();
        builder.Property(e => e.WarningCount).IsRequired();
        builder.Property(e => e.ExcludedCount).IsRequired();
        builder.Property(e => e.LineCount).IsRequired();
        builder.Property(e => e.TotalsJson).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(RegulatoryFile.LargoDelNombreDeArchivo);
        builder.Property(e => e.FileSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.GeneratedAt).IsRequired();
        builder.Property(e => e.FilingNumber).HasMaxLength(RegulatoryFile.LargoDelRadicado);
        builder.Ignore(e => e.TieneArchivo);

        builder.HasOne<MonthlyClose>().WithMany().HasForeignKey(e => e.MonthlyCloseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PeriodYear, e.PeriodMonth, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_RegulatoryFiles_Sicses_Period_Version").HasFilter("[Kind] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.BureauCode, e.PeriodYear, e.PeriodMonth, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_RegulatoryFiles_Bureau_Period_Version").HasFilter("[Kind] = 2 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.FormatCode, e.PeriodYear, e.PeriodMonth, e.Version }).IsUnique()
            .HasDatabaseName("UK_LND_RegulatoryFiles_Format_Period_Version").HasFilter("[FormatCode] IS NOT NULL AND [IsDeleted] = 0");
    }
}

/// <summary>
/// <c>LND_RegulatoryFileLines</c> (feature 013, K5, US16; data-model §28.2): hecho <c>bigint</c> hijo del archivo de una central;
/// <c>UK (RegulatoryFileId, LoanId, PersonId)</c> —una obligación y titular a lo sumo una vez por archivo— y número de línea único; un excluido
/// lleva su motivo (CHECK). FK <c>Restrict</c> al archivo, al crédito, a la persona y al aviso previo del episodio.
/// </summary>
public class RegulatoryFileLineConfiguration : IEntityTypeConfiguration<RegulatoryFileLine>
{
    public const string Tabla = "LND_RegulatoryFileLines";

    public void Configure(EntityTypeBuilder<RegulatoryFileLine> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_RegulatoryFileLines_ExclusionReason", "[Included] = 1 OR [ExclusionReason] IS NOT NULL"));

        builder.Property(e => e.LineNumber).IsRequired();
        builder.Property(e => e.Included).IsRequired();
        builder.Property(e => e.ExclusionReason).HasMaxLength(RegulatoryFileLine.LargoDelMotivo);
        builder.Property(e => e.ReportedStatusCode).HasMaxLength(RegulatoryFileLine.LargoDelEstado);
        builder.Property(e => e.DaysPastDue).IsRequired();
        builder.Property(e => e.BalanceReported).Monto();
        builder.Property(e => e.IsNegative).IsRequired();
        builder.Property(e => e.HasClaimInProcess).IsRequired();
        builder.Property(e => e.IsWithdrawalOfUnnoticedReport).IsRequired();

        builder.HasOne<RegulatoryFile>().WithMany().HasForeignKey(e => e.RegulatoryFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PriorNotice>().WithMany().HasForeignKey(e => e.PriorNoticeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RegulatoryFileId, e.LoanId, e.PersonId }).IsUnique().HasDatabaseName("UK_LND_RegulatoryFileLines_File_Loan_Person");
        builder.HasIndex(e => new { e.RegulatoryFileId, e.LineNumber }).IsUnique().HasDatabaseName("UK_LND_RegulatoryFileLines_File_Line");
        builder.HasIndex(e => new { e.LoanId, e.PersonId }).HasDatabaseName("IX_LND_RegulatoryFileLines_Loan_Person");
    }
}
