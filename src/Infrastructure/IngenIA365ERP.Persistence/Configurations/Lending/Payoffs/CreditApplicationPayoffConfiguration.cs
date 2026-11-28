using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Payoffs;

// 2026-11-27 (pedido del dueño; feature 028): la recogida proyectada de la solicitud (migración RecogidaDeCreditos, aditiva).

/// <summary>
/// <c>LND_CreditApplicationPayoffs</c>: una fila por solicitud y producto. Un único filtrado entre las vivas por cada clase de producto —el crédito, la
/// cuenta o la factura no se repiten en la misma solicitud— (tres índices y no uno: con un único sobre columnas que admiten nulos, PostgreSQL no ve
/// repetidos los nulos y el índice no cuidaría nada); el índice por crédito sirve al aviso de «ya está en otra solicitud» (FR-009). El CHECK exige el
/// identificador de la clase y sólo ése. FK <c>Restrict</c>; la factura es dato (es saldo de Contabilidad).
/// </summary>
public class CreditApplicationPayoffConfiguration : IEntityTypeConfiguration<CreditApplicationPayoff>
{
    public const string Tabla = "LND_CreditApplicationPayoffs";

    public void Configure(EntityTypeBuilder<CreditApplicationPayoff> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_CreditApplicationPayoffs_Product",
                "([ProductKind] = 1 AND [LoanId] IS NOT NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NULL)"
                + " OR ([ProductKind] = 2 AND [LoanId] IS NULL AND [AccountId] IS NOT NULL AND [CommercialReceivableReference] IS NULL)"
                + " OR ([ProductKind] = 3 AND [LoanId] IS NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NOT NULL)");
        });

        builder.Property(e => e.ProductKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CommercialReceivableReference).HasMaxLength(ProductoRecogido.LargoDeLaReferencia);
        builder.Property(e => e.ProductNumber).HasMaxLength(ProductoRecogido.LargoDelNumero).IsRequired();
        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedAmount).Monto();
        builder.Property(e => e.InstallmentTreatment).HasConversion<int?>();
        builder.Property(e => e.InterestMode).HasMaxLength(CreditApplicationPayoff.LargoDelModoDeIntereses);
        builder.Property(e => e.ProjectedDate).IsRequired();
        builder.Property(e => e.RecordedAsOf).IsRequired();
        builder.Property(e => e.ProjectedAmount).Monto().IsRequired();
        builder.Property(e => e.ProjectedBreakdown).IsRequired();
        builder.Property(e => e.CurrentInstallmentAmount).Monto();
        builder.Property(e => e.SentInstallmentState).HasConversion<int>().IsRequired();
        builder.Property(e => e.ProjectionSha256).HasMaxLength(CreditApplicationPayoff.LargoDeLaHuella).IsRequired();
        builder.Property(e => e.Order).IsRequired();
        builder.Ignore(e => e.TieneProyeccion);

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.CreditApplicationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_CreditApplicationPayoffs_Application");
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_CreditApplicationPayoffs_Loan");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_CreditApplicationPayoffs_Account");
        builder.HasOne<PayrollDeductionFile>().WithMany().HasForeignKey(e => e.PayrollDeductionFileId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_CreditApplicationPayoffs_PayrollDeductionFile");

        builder.HasIndex(e => new { e.CreditApplicationId, e.LoanId }).IsUnique()
            .HasDatabaseName("UK_LND_CreditApplicationPayoffs_Application_Loan").HasFilter("[LoanId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.CreditApplicationId, e.AccountId }).IsUnique()
            .HasDatabaseName("UK_LND_CreditApplicationPayoffs_Application_Account").HasFilter("[AccountId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.CreditApplicationId, e.CommercialReceivableReference }).IsUnique()
            .HasDatabaseName("UK_LND_CreditApplicationPayoffs_Application_Receivable")
            .HasFilter("[CommercialReceivableReference] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_CreditApplicationPayoffs_LoanId");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_LND_CreditApplicationPayoffs_AccountId");
        builder.HasIndex(e => e.PayrollDeductionFileId).HasDatabaseName("IX_LND_CreditApplicationPayoffs_PayrollDeductionFileId");
    }
}
