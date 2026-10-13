using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Integration;

/// <summary>
/// <c>LND_AccountingFactParts</c> (feature 013, C.3; data-model §22.3): hecho sin diff. Una parte por tanda, sucursal y tipo de
/// hecho; las FK <c>MonthlyCloseId</c> y <c>MonthlyCloseReopeningId</c> las trae <c>CarteraRiesgo</c> (C66, US9 T0697).
/// </summary>
public class AccountingFactPartConfiguration : IEntityTypeConfiguration<AccountingFactPart>
{
    public const string Tabla = "LND_AccountingFactParts";

    public void Configure(EntityTypeBuilder<AccountingFactPart> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.SourceKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.MonthlyCloseId);
        builder.Property(e => e.MonthlyCloseReopeningId);
        builder.Property(e => e.FactType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.PerLoanMessages).IsRequired();
        builder.Property(e => e.ValueDate).IsRequired();
        builder.Property(e => e.AccountingDate).IsRequired();
        builder.Property(e => e.OriginEventKey).HasMaxLength(80).IsRequired();
        builder.Property(e => e.MessageCount).IsRequired();
        builder.Property(e => e.LoanCount).IsRequired();
        builder.Property(e => e.TotalAmount).Monto().IsRequired();
        builder.Property(e => e.TotalsByDimensionJson).IsRequired();
        builder.Property(e => e.LoanListSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.PrevalidationOutcome).HasMaxLength(20);

        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LoanOperation>().WithMany().HasForeignKey(e => e.TriggerOperationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountingFactPart>().WithMany().HasForeignKey(e => e.ReversedPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        // US9 (T0697, C66): las que la base dejó sin crear hasta que existieran el cierre y su reapertura.
        builder.HasOne<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyClose>().WithMany().HasForeignKey(e => e.MonthlyCloseId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IngenIA365ERP.Domain.Entities.Lending.Risk.MonthlyCloseReopening>().WithMany().HasForeignKey(e => e.MonthlyCloseReopeningId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique()
            .HasDatabaseName("UK_LND_AccountingFactParts_MessagePublicId").HasFilter("[MessagePublicId] IS NOT NULL");
        builder.HasIndex(e => new { e.ProcessRunId, e.BatchNumber, e.BranchId, e.FactType }).IsUnique()
            .HasDatabaseName("UK_LND_AccountingFactParts_Batch").HasFilter("[ProcessRunId] IS NOT NULL");
        builder.HasIndex(e => new { e.TriggerOperationId, e.BranchId, e.FactType }).IsUnique()
            .HasDatabaseName("UK_LND_AccountingFactParts_Trigger_Branch_Fact").HasFilter("[TriggerOperationId] IS NOT NULL");
        builder.HasIndex(e => new { e.AccountingDate, e.FactType }).HasDatabaseName("IX_LND_AccountingFactParts_AccountingDate_FactType");
        builder.HasIndex(e => e.MonthlyCloseId).HasDatabaseName("IX_LND_AccountingFactParts_MonthlyCloseId");
        builder.HasIndex(e => e.MonthlyCloseReopeningId).HasDatabaseName("IX_LND_AccountingFactParts_MonthlyCloseReopeningId");
    }
}
