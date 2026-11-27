using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Payoffs;

// 2026-11-27 (pedido del dueño; feature 028): la recogida aplicada en el desembolso (migración RecogidaDeCreditos, aditiva).

/// <summary>
/// <c>LND_DisbursementPayoffs</c>: una fila por operación de desembolso y producto. La clave con que se aplicó es única entre las vivas (es la del
/// contrato, <c>CAR:{op:N}:{producto:N}</c>); el índice por crédito sirve al «recogido por el crédito …» y al informe. El CHECK exige el identificador de
/// la clase y sólo ése. FK <c>Restrict</c> a la operación de desembolso y a la de pago (<c>LND_LoanOperations</c>), a la proyectada, al crédito, a la
/// cuenta y a su operación; el recibo de Recaudo es dato.
/// </summary>
public class DisbursementPayoffConfiguration : IEntityTypeConfiguration<DisbursementPayoff>
{
    public const string Tabla = "LND_DisbursementPayoffs";

    public void Configure(EntityTypeBuilder<DisbursementPayoff> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_DisbursementPayoffs_Product",
                "([ProductKind] = 1 AND [LoanId] IS NOT NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NULL)"
                + " OR ([ProductKind] = 2 AND [LoanId] IS NULL AND [AccountId] IS NOT NULL AND [CommercialReceivableReference] IS NULL)"
                + " OR ([ProductKind] = 3 AND [LoanId] IS NULL AND [AccountId] IS NULL AND [CommercialReceivableReference] IS NOT NULL)");
        });

        builder.Property(e => e.ProductKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.CommercialReceivableReference).HasMaxLength(ProductoRecogido.LargoDeLaReferencia);
        builder.Property(e => e.ProductNumber).HasMaxLength(ProductoRecogido.LargoDelNumero).IsRequired();
        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.InstallmentTreatment).HasConversion<int?>();
        builder.Property(e => e.InterestMode).HasMaxLength(CreditApplicationPayoff.LargoDelModoDeIntereses);
        builder.Property(e => e.ProjectedAmount).Monto().IsRequired();
        builder.Property(e => e.CurrentAmount).Monto().IsRequired();
        builder.Property(e => e.AppliedAmount).Monto().IsRequired();
        builder.Property(e => e.Difference).Monto().IsRequired();
        builder.Property(e => e.DifferenceCause).HasConversion<int>().IsRequired();
        builder.Property(e => e.DifferenceDetail).IsRequired();
        builder.Property(e => e.Omitted).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(DisbursementPayoff.LargoDeLaClave).IsRequired();
        builder.Property(e => e.ReversalKey).HasMaxLength(DisbursementPayoff.LargoDeLaClaveDeReverso);
        builder.Property(e => e.AcknowledgedBy).HasMaxLength(DisbursementPayoff.LargoDeQuienReconoce);
        builder.Ignore(e => e.EstaReversada);

        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.DisbursementOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DisbursementPayoffs_DisbursementOperation");
        builder.HasOne<CreditApplicationPayoff>().WithMany().HasForeignKey(e => e.CreditApplicationPayoffId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DisbursementPayoffs_ApplicationPayoff");
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DisbursementPayoffs_Loan");
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DisbursementPayoffs_Account");
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.PaymentOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DisbursementPayoffs_PaymentOperation");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.AccountOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DisbursementPayoffs_AccountOperation");

        builder.HasIndex(e => e.IdempotencyKey).IsUnique()
            .HasDatabaseName("UK_LND_DisbursementPayoffs_IdempotencyKey").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.DisbursementOperationId).HasDatabaseName("IX_LND_DisbursementPayoffs_DisbursementOperationId");
        builder.HasIndex(e => e.CreditApplicationPayoffId).HasDatabaseName("IX_LND_DisbursementPayoffs_CreditApplicationPayoffId");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_DisbursementPayoffs_LoanId");
        builder.HasIndex(e => e.AccountId).HasDatabaseName("IX_LND_DisbursementPayoffs_AccountId");
        builder.HasIndex(e => e.PaymentOperationId).HasDatabaseName("IX_LND_DisbursementPayoffs_PaymentOperationId");
        builder.HasIndex(e => e.AccountOperationId).HasDatabaseName("IX_LND_DisbursementPayoffs_AccountOperationId");
    }
}
