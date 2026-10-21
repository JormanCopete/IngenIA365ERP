using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

// Feature 014, US5 (T130; data-model §4.5, §4.6): la liquidación de intereses y la retención acumulada por titular, año y concepto. Migración
// CaptacionesAportes (research E35, §14). (nuevo)

/// <summary>
/// <c>LND_InterestSettlements</c> (data-model §4.5): una por cuenta y período vivo (<c>UK_LND_InterestSettlements_Account_Period</c> filtrado a las no
/// reversadas, SC-003); una por operación. Lo causado y su diferencia de redondeo llevan los seis decimales de la causación.
/// </summary>
public class InterestSettlementConfiguration : IEntityTypeConfiguration<InterestSettlement>
{
    public const string Tabla = "LND_InterestSettlements";

    public void Configure(EntityTypeBuilder<InterestSettlement> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t => t.HasCheckConstraint("CK_LND_InterestSettlements_Period", "[PeriodTo] >= [PeriodFrom]"));

        builder.Property(e => e.AccruedInterest).MontoCausado().IsRequired();
        builder.Property(e => e.GrossInterest).Monto().IsRequired();
        builder.Property(e => e.RoundingDifference).MontoCausado().IsRequired();
        builder.Property(e => e.ThresholdDailyAmount).Monto();
        builder.Property(e => e.InflationaryComponentFraction).Fraccion();
        builder.Property(e => e.InflationaryComponentAmount).Monto();
        builder.Property(e => e.WithholdingBase).Monto().IsRequired();
        builder.Property(e => e.WithholdingRate).TasaAnual().IsRequired();
        builder.Property(e => e.WithholdingAmount).Monto().IsRequired();
        builder.Property(e => e.WithholdingExemptionReason).HasMaxLength(InterestSettlement.LargoDelMotivo);
        builder.Property(e => e.NetInterest).Monto().IsRequired();
        builder.Property(e => e.Destination).HasConversion<int>().IsRequired();
        builder.Property(e => e.TaxProfileSnapshotJson).IsRequired();
        builder.Property(e => e.InputsSha256).HasMaxLength(64).IsRequired();

        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccountOperation>().WithMany().HasForeignKey(e => e.ReversedByOperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_InterestSettlements_ReversedBy");

        builder.HasIndex(e => new { e.MemberAccountId, e.PeriodTo }).IsUnique().HasDatabaseName("UK_LND_InterestSettlements_Account_Period")
            .HasFilter("[ReversedByOperationId] IS NULL");
        builder.HasIndex(e => e.OperationId).IsUnique().HasDatabaseName("UK_LND_InterestSettlements_OperationId");
        builder.HasIndex(e => e.ReversedByOperationId).HasDatabaseName("IX_LND_InterestSettlements_ReversedBy").HasFilter("[ReversedByOperationId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_WithholdingAccumulations</c> (data-model §4.6): hecho <c>bigint</c> por liquidación y parte; el acumulado del certificado es Σ por persona y año.
/// </summary>
public class WithholdingAccumulationConfiguration : IEntityTypeConfiguration<WithholdingAccumulation>
{
    public const string Tabla = "LND_WithholdingAccumulations";

    public void Configure(EntityTypeBuilder<WithholdingAccumulation> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);

        builder.Property(e => e.DepositClass).HasConversion<int?>();
        builder.Property(e => e.Share).Fraccion().IsRequired();
        builder.Property(e => e.GrossInterest).Monto().IsRequired();
        builder.Property(e => e.InflationaryComponentAmount).Monto().IsRequired();
        builder.Property(e => e.WithholdingBase).Monto().IsRequired();
        builder.Property(e => e.WithholdingAmount).Monto().IsRequired();
        builder.Property(e => e.Origin).HasMaxLength(20).IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MemberAccount>().WithMany().HasForeignKey(e => e.MemberAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.InterestSettlement).WithMany().HasForeignKey(e => e.InterestSettlementId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_WithholdingAccumulations_Settlement");
        builder.HasOne(e => e.Operation).WithMany().HasForeignKey(e => e.OperationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_WithholdingAccumulations_Operation");
        builder.HasOne<WithholdingAccumulation>().WithMany().HasForeignKey(e => e.ReversesAccumulationId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_WithholdingAccumulations_Reverses");

        builder.HasIndex(e => new { e.PersonId, e.TaxYear }).HasDatabaseName("IX_LND_WithholdingAccumulations_Person_Year");
        builder.HasIndex(e => new { e.MemberAccountId, e.TaxYear }).HasDatabaseName("IX_LND_WithholdingAccumulations_Account_Year");
        builder.HasIndex(e => e.InterestSettlementId).HasDatabaseName("IX_LND_WithholdingAccumulations_Settlement");
        builder.HasIndex(e => e.OperationId).HasDatabaseName("IX_LND_WithholdingAccumulations_OperationId");
        builder.HasIndex(e => e.ReversesAccumulationId).IsUnique().HasDatabaseName("UK_LND_WithholdingAccumulations_Reverses")
            .HasFilter("[ReversesAccumulationId] IS NOT NULL");
    }
}
