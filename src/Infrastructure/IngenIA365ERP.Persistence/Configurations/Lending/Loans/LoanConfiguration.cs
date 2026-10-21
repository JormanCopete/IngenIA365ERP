using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Loans;

/// <summary>
/// <c>LND_Loans</c> (feature 013; data-model §4.1): número único sin filtro; ninguna venta origina dos obligaciones (único del pago de
/// Comercial filtrado a no nulo); índices por deudor, oficina y línea con el estado, y por factura.
/// </summary>
public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public const string Tabla = "LND_Loans";

    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Number).HasMaxLength(20).IsRequired();
        builder.Property(e => e.DebtorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Origin).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ApprovedAmount).Monto().IsRequired();
        builder.Property(e => e.OpenedOn).IsRequired();
        builder.Property(e => e.SourceInvoiceNumber).HasMaxLength(30);
        builder.Property(e => e.AccountsReceivableRecordedBy).HasMaxLength(20);
        builder.Property(e => e.SaleObligationStatus).HasConversion<int?>();
        builder.Property(e => e.ValidationFailureCode).HasMaxLength(80);
        builder.Property(e => e.ValidationFailureReason).HasMaxLength(500);
        builder.Property(e => e.OriginLinkKind).HasConversion<int?>();
        builder.Property(e => e.VoidReason).HasMaxLength(500);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.DebtorPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanNumberReservation>().WithMany().HasForeignKey(e => e.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLimit>().WithMany().HasForeignKey(e => e.CreditLimitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.ConfirmedByExceptionDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.OriginLoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MigrationLoad>().WithMany().HasForeignKey(e => e.MigrationLoadId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_Loans_Number");
        builder.HasIndex(e => e.SourcePaymentPublicId).IsUnique()
            .HasDatabaseName("UK_LND_Loans_SourcePayment").HasFilter("[SourcePaymentPublicId] IS NOT NULL");
        builder.HasIndex(e => new { e.DebtorPersonId, e.Status }).HasDatabaseName("IX_LND_Loans_Debtor_Status");
        builder.HasIndex(e => new { e.BranchId, e.Status }).HasDatabaseName("IX_LND_Loans_Branch_Status");
        builder.HasIndex(e => new { e.CreditLineId, e.Status }).HasDatabaseName("IX_LND_Loans_Line_Status");
        builder.HasIndex(e => e.SourceInvoiceNumber).HasDatabaseName("IX_LND_Loans_SourceInvoiceNumber");
    }
}
