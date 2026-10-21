using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

// Feature 014, US4 (T110, T114, T118; data-model §2.3, §3.6, §4.7, §4.8, §4.9): lo que suman los movimientos de las cuentas —el GMF del mes, los cargos,
// sus exoneraciones y el débito automático—. Migración CaptacionesAportes (research E35, §12). (nuevo)

/// <summary>
/// <c>LND_GmfMonthlyUsage</c> (data-model §4.7): hecho <c>bigint</c> por disposición; el acumulado del mes por persona es Σ, por eso el índice
/// <c>(PersonId, Year, Month)</c>. Un reverso por fila.
/// </summary>
public class GmfMonthlyUsageConfiguration : IEntityTypeConfiguration<GmfMonthlyUsage>
{
    public const string Tabla = "LND_GmfMonthlyUsage";

    public void Configure(EntityTypeBuilder<GmfMonthlyUsage> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_GmfMonthlyUsage_Month", "[Month] BETWEEN 1 AND 12"));

        builder.Property(e => e.TaxableEventCode).HasMaxLength(GmfMonthlyUsage.LargoDelHecho).IsRequired();
        builder.Property(e => e.DisposedAmount).Monto().IsRequired();
        builder.Property(e => e.ExemptAmount).Monto().IsRequired();
        builder.Property(e => e.TaxableBase).Monto().IsRequired();
        builder.Property(e => e.GmfAmount).Monto().IsRequired();
        builder.Property(e => e.Policy).HasConversion<int>().IsRequired();
        builder.Property(e => e.Rate).TasaAnual().IsRequired();
        builder.Property(e => e.Origin).HasMaxLength(20).IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GmfMonthlyUsage>().WithMany().HasForeignKey(e => e.ReversesUsageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PersonId, e.Year, e.Month }).HasDatabaseName("IX_LND_GmfMonthlyUsage_Person_Month");
        builder.HasIndex(e => e.MemberAccountId).HasDatabaseName("IX_LND_GmfMonthlyUsage_MemberAccountId");
        builder.HasIndex(e => e.OperationId).HasDatabaseName("IX_LND_GmfMonthlyUsage_OperationId");
        builder.HasIndex(e => e.ReversesUsageId).IsUnique().HasDatabaseName("UK_LND_GmfMonthlyUsage_Reverses").HasFilter("[ReversesUsageId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_DepositLineCharges</c> (data-model §2.3): hija de la versión de captación, hecho de sólo inserción; código único por versión.
/// </summary>
public class DepositLineChargeConfiguration : IEntityTypeConfiguration<DepositLineCharge>
{
    public const string Tabla = "LND_DepositLineCharges";

    public void Configure(EntityTypeBuilder<DepositLineCharge> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(DepositLineCharge.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(DepositLineCharge.LargoDelNombre).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Trigger).HasConversion<int>().IsRequired();
        builder.Property(e => e.Periodicity).HasConversion<int?>();
        builder.Property(e => e.FreePeriod).HasConversion<int?>();
        builder.Property(e => e.CalculationMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.FixedAmount).Monto();
        builder.Property(e => e.Percentage).Fraccion();
        builder.Property(e => e.PercentageBase).HasConversion<int?>();
        builder.Property(e => e.MinAmount).Monto();
        builder.Property(e => e.MaxAmount).Monto();
        builder.Property(e => e.ExemptIfAverageBalanceAtLeast).Monto();
        builder.Property(e => e.ExemptIfMinimumBalanceAtLeast).Monto();
        builder.Property(e => e.ShortfallPolicy).HasConversion<int>().IsRequired();
        builder.Property(e => e.CommercialItemCode).HasMaxLength(DepositLineCharge.LargoDelItemComercial);

        builder.HasOne<DepositLineVersion>().WithMany().HasForeignKey(e => e.DepositLineVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_DepositLineCharges_Version");

        builder.HasIndex(e => new { e.DepositLineVersionId, e.Code }).IsUnique().HasDatabaseName("UK_LND_DepositLineCharges_Version_Code");
    }
}

/// <summary>
/// <c>LND_AccountCharges</c> (data-model §4.8): documento con estado. Único por cuenta, cargo y período (periódicos) y por operación y cargo (por
/// evento).
/// </summary>
public class AccountChargeConfiguration : IEntityTypeConfiguration<AccountCharge>
{
    public const string Tabla = "LND_AccountCharges";

    public void Configure(EntityTypeBuilder<AccountCharge> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ChargeCode).HasMaxLength(DepositLineCharge.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.BaseAmount).Monto().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.WaiverReason).HasMaxLength(AccountCharge.LargoDelMotivo);
        builder.Property(e => e.CommercialInvoiceNumber).HasMaxLength(40);
        builder.Property(e => e.CommercialInvoiceStatus).HasMaxLength(20);

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DepositLineCharge>().WithMany().HasForeignKey(e => e.DepositLineChargeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.TriggerOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountCharges_TriggerOperation");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.AppliedOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountCharges_AppliedOperation");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.CollectedOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountCharges_CollectedOperation");
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.ReversedByOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_AccountCharges_ReversedByOperation");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.WaivedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.WaiverDecisionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MemberAccountId, e.DepositLineChargeId, e.PeriodFrom }).IsUnique()
            .HasDatabaseName("UK_LND_AccountCharges_Account_Charge_Period").HasFilter("[PeriodFrom] IS NOT NULL");
        builder.HasIndex(e => new { e.TriggerOperationId, e.DepositLineChargeId }).IsUnique()
            .HasDatabaseName("UK_LND_AccountCharges_Trigger_Charge").HasFilter("[TriggerOperationId] IS NOT NULL");
        builder.HasIndex(e => new { e.MemberAccountId, e.Status }).HasDatabaseName("IX_LND_AccountCharges_Account_Status");
        builder.HasIndex(e => e.AppliedOperationId).HasDatabaseName("IX_LND_AccountCharges_AppliedOperationId");
        builder.HasIndex(e => e.CollectedOperationId).HasDatabaseName("IX_LND_AccountCharges_CollectedOperationId")
            .HasFilter("[CollectedOperationId] IS NOT NULL");
        builder.HasIndex(e => e.ChargeMessagePublicId).HasDatabaseName("IX_LND_AccountCharges_ChargeMessage")
            .HasFilter("[ChargeMessagePublicId] IS NOT NULL");
    }
}

/// <summary><c>LND_AccountChargeExemptions</c> (data-model §4.9): una exoneración vigente por cuenta y código.</summary>
public class AccountChargeExemptionConfiguration : IEntityTypeConfiguration<AccountChargeExemption>
{
    public const string Tabla = "LND_AccountChargeExemptions";

    public void Configure(EntityTypeBuilder<AccountChargeExemption> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ChargeCode).HasMaxLength(DepositLineCharge.LargoDelCodigo).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(AccountChargeExemption.LargoDelMotivo).IsRequired();

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditDecision>().WithMany().HasForeignKey(e => e.DecisionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MemberAccountId, e.ChargeCode }).IsUnique().HasDatabaseName("UK_LND_AccountChargeExemptions_Account_Code")
            .HasFilter("[ValidTo] IS NULL AND [IsDeleted] = 0");
    }
}

/// <summary>
/// <c>LND_DirectDebitMandates</c> (data-model §3.6): estado con fechas; una autorización vigente por cuenta y crédito.
/// </summary>
public class DirectDebitMandateConfiguration : IEntityTypeConfiguration<DirectDebitMandate>
{
    public const string Tabla = "LND_DirectDebitMandates";

    public void Configure(EntityTypeBuilder<DirectDebitMandate> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Scope).HasConversion<int>().IsRequired();
        builder.Property(e => e.AuthorizationAttachmentPublicId).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.RevokeReason).HasMaxLength(DirectDebitMandate.LargoDelMotivo);

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.AuthorizedByPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Loan>().WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MemberAccountId, e.LoanId }).IsUnique().HasDatabaseName("UK_LND_DirectDebitMandates_Account_Loan")
            .HasFilter("[ValidTo] IS NULL AND [LoanId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(e => e.LoanId).HasDatabaseName("IX_LND_DirectDebitMandates_LoanId").HasFilter("[LoanId] IS NOT NULL");
        builder.HasIndex(e => e.AuthorizedByPersonId).HasDatabaseName("IX_LND_DirectDebitMandates_AuthorizedByPersonId");
    }
}
