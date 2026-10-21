using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Integration;

/// <summary>
/// <c>LND_ProcessRuns</c> (feature 013, C.3; data-model §22.1): un ciclo por día, una corrida por versión de cierre y por reapertura,
/// y una sola corrida <c>Running</c> por cooperativa (únicos filtrados).
/// </summary>
public class LendingProcessRunConfiguration : IEntityTypeConfiguration<LendingProcessRun>
{
    public const string Tabla = "LND_ProcessRuns";

    public void Configure(EntityTypeBuilder<LendingProcessRun> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedByKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedByName).HasMaxLength(150);
        builder.Property(e => e.RequestedByIp).HasMaxLength(45);
        builder.Property(e => e.ExecutionOrigin).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.TotalLoans).IsRequired();
        builder.Property(e => e.ProcessedLoans).IsRequired();
        builder.Property(e => e.FailedLoans).IsRequired();
        builder.Property(e => e.SkippedLoans).IsRequired();
        builder.Property(e => e.BatchCount).IsRequired();
        builder.Property(e => e.PartCount).IsRequired();
        builder.Property(e => e.RetryCount).IsRequired();
        builder.Property(e => e.TotalsJson);
        builder.Property(e => e.ResultSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.RequestedAt).IsRequired();

        builder.HasOne<NationalValueReplication>().WithMany().HasForeignKey(e => e.NationalValueReplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Kind, e.ProcessDate }).IsUnique()
            .HasDatabaseName("UK_LND_ProcessRuns_DailyCycle").HasFilter("[Kind] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Kind, e.PeriodYear, e.PeriodMonth, e.CloseVersion }, "UK_LND_ProcessRuns_Close")
            .IsUnique().HasFilter("[Kind] = 2 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Kind, e.PeriodYear, e.PeriodMonth, e.CloseVersion }, "UK_LND_ProcessRuns_Reopen")
            .IsUnique().HasFilter("[Kind] = 3 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.Status).IsUnique()
            .HasDatabaseName("UK_LND_ProcessRuns_Running").HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Status, e.RequestedAt }).HasDatabaseName("IX_LND_ProcessRuns_Status_RequestedAt");
    }
}
