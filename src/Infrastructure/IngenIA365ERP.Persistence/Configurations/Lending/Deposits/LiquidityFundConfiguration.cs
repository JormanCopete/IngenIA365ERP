using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Liquidity;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

/// <summary>
/// <c>LND_LiquidityFundSnapshots</c> (feature 014, US15, T215; data-model §6.7): la foto del fondo de liquidez, hecho de sólo inserción, una por fecha
/// (<c>UK_LND_LiquidityFundSnapshots_Date_Version</c>, sin filtro de baja: es un hecho). Migración <c>CaptacionesAportes</c> (E35). (nuevo)
/// </summary>
public class LiquidityFundSnapshotConfiguration : IEntityTypeConfiguration<LiquidityFundSnapshot>
{
    public const string Tabla = "LND_LiquidityFundSnapshots";

    public void Configure(EntityTypeBuilder<LiquidityFundSnapshot> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_LiquidityFundSnapshots_Version", "[Version] >= 1");
            t.HasCheckConstraint("CK_LND_LiquidityFundSnapshots_Amounts", "[RequiredAmount] >= 0 AND [ConstitutedAmount] >= 0 AND [Shortfall] >= 0");
        });

        builder.Property(e => e.Category).HasMaxLength(LiquidityFundSnapshot.LargoDeLaCategoria).IsRequired();
        builder.Property(e => e.RegimeStage).HasMaxLength(LiquidityFundSnapshot.LargoDelRegimen).IsRequired();
        builder.Property(e => e.BaseByClassJson).IsRequired();
        builder.Property(e => e.RequiredAmount).Monto().IsRequired();
        builder.Property(e => e.NewRegimeRequiredAmount).Monto();
        builder.Property(e => e.ConstitutedAmount).Monto().IsRequired();
        builder.Property(e => e.Shortfall).Monto().IsRequired();
        builder.Property(e => e.MinimumIrl).HasPrecision(18, 4);
        builder.Property(e => e.ParametersSnapshotJson).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(LiquidityFundSnapshot.LargoDelMotivo);
        builder.Property(e => e.InputsSha256).HasMaxLength(64).IsRequired();

        builder.HasOne<LendingProcessRun>().WithMany().HasForeignKey(e => e.ProcessRunId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_LiquidityFundSnapshots_ProcessRun");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_LiquidityFundSnapshots_RequestedBy");

        builder.HasIndex(e => new { e.SnapshotDate, e.Version }).IsUnique().HasDatabaseName("UK_LND_LiquidityFundSnapshots_Date_Version");
        builder.HasIndex(e => e.ProcessRunId).HasDatabaseName("IX_LND_LiquidityFundSnapshots_ProcessRun").HasFilter("[ProcessRunId] IS NOT NULL");
    }
}

/// <summary>
/// <c>LND_LiquidityFundHoldings</c> (feature 014, US15, T215; data-model §6.8): lo constituido del fondo de liquidez, con su entidad depositaria y su soporte;
/// el índice por fechas sirve a «el constituido de un día». (nuevo)
/// </summary>
public class LiquidityFundHoldingConfiguration : IEntityTypeConfiguration<LiquidityFundHolding>
{
    public const string Tabla = "LND_LiquidityFundHoldings";

    public void Configure(EntityTypeBuilder<LiquidityFundHolding> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LND_LiquidityFundHoldings_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_LND_LiquidityFundHoldings_Dates", "[WithdrawnOn] IS NULL OR [WithdrawnOn] >= [ConstitutedOn]");
        });

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(LiquidityFundHolding.LargoDeLaReferencia).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.Note).HasMaxLength(LiquidityFundHolding.LargoDeLaNota).IsRequired();
        builder.Property(e => e.EndReason).HasMaxLength(LiquidityFundHolding.LargoDeLaNota);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.InstitutionPersonId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_LiquidityFundHoldings_Institution");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_LiquidityFundHoldings_RegisteredBy");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EndedByUserId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LND_LiquidityFundHoldings_EndedBy");

        builder.HasIndex(e => new { e.ConstitutedOn, e.WithdrawnOn }).HasDatabaseName("IX_LND_LiquidityFundHoldings_Dates");
        builder.HasIndex(e => e.InstitutionPersonId).HasDatabaseName("IX_LND_LiquidityFundHoldings_Institution");
    }
}
