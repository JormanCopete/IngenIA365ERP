using IngenIA365ERP.Domain.Entities.Payroll.Pila;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Datos del aportante (feature 010, US5; data-model §2.8): fila única por cooperativa (regla del comando).</summary>
public class PilaSettingsConfiguration : IEntityTypeConfiguration<PilaSettings>
{
    public void Configure(EntityTypeBuilder<PilaSettings> builder)
    {
        builder.ToTable("PAY_PilaSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_PilaSettings_PublicId");

        builder.Property(e => e.ContributorType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ContributorClass).HasMaxLength(1).IsRequired();
        builder.Property(e => e.PresentationForm).HasMaxLength(1).IsRequired();
        builder.Property(e => e.BranchCode).HasMaxLength(10);
        builder.Property(e => e.BranchName).HasMaxLength(40);
        builder.Property(e => e.ArlPilaCode).HasMaxLength(6);
        builder.Property(e => e.MunicipalityDaneCode).HasMaxLength(5);
        builder.Property(e => e.EconomicActivityCode).HasMaxLength(7);
        builder.Property(e => e.OperatorName).HasMaxLength(40);
        builder.Property(e => e.OperatorCode).HasMaxLength(2);
        builder.Property(e => e.PlanillaType).HasMaxLength(1).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Generación de la planilla (inmutable): única por período y versión.</summary>
public class PilaGenerationConfiguration : IEntityTypeConfiguration<PilaGeneration>
{
    public void Configure(EntityTypeBuilder<PilaGeneration> builder)
    {
        builder.ToTable("PAY_PilaGenerations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_PilaGenerations_PublicId");

        builder.Property(e => e.LayoutVersion).HasMaxLength(40).IsRequired();
        builder.Property(e => e.GeneratedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(120);
        builder.Property(e => e.FileSha256).HasMaxLength(64);
        builder.Property(e => e.UploadedBy).HasMaxLength(100);
        builder.Property(e => e.OperatorFilingNumber).HasMaxLength(40);
        foreach (var p in new[] { nameof(PilaGeneration.TotalIbcHealth), nameof(PilaGeneration.TotalIbcPension), nameof(PilaGeneration.TotalIbcWorkRisk), nameof(PilaGeneration.TotalIbcFamilyCompensation),
                                  nameof(PilaGeneration.TotalHealth), nameof(PilaGeneration.TotalPension), nameof(PilaGeneration.TotalSolidarityFund), nameof(PilaGeneration.TotalWorkRisk),
                                  nameof(PilaGeneration.TotalFamilyCompensation), nameof(PilaGeneration.TotalSena), nameof(PilaGeneration.TotalIcbf), nameof(PilaGeneration.TotalContributions) })
            builder.Property(p).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.Year, e.Month, e.Version }).IsUnique().HasDatabaseName("UK_PAY_PilaGenerations_Period_Version");
        builder.HasIndex(e => new { e.Year, e.Month, e.Status }).HasDatabaseName("IX_PAY_PilaGenerations_Period_Status");

        builder.HasMany(e => e.Lines).WithOne(l => l.Generation).HasForeignKey(l => l.GenerationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Issues).WithOne(i => i.Generation).HasForeignKey(i => i.GenerationId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PilaGenerationLineConfiguration : IEntityTypeConfiguration<PilaGenerationLine>
{
    public void Configure(EntityTypeBuilder<PilaGenerationLine> builder)
    {
        builder.ToTable("PAY_PilaGenerationLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_PilaGenerationLines_PublicId");

        builder.Property(e => e.ContributorType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.ContributorSubType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.NoveltyFlags).HasMaxLength(60).IsRequired();
        builder.Property(e => e.RecordText).HasMaxLength(800).IsRequired();
        foreach (var p in new[] { nameof(PilaGenerationLine.Salary), nameof(PilaGenerationLine.IbcHealth), nameof(PilaGenerationLine.IbcPension), nameof(PilaGenerationLine.IbcWorkRisk), nameof(PilaGenerationLine.IbcFamilyCompensation),
                                  nameof(PilaGenerationLine.Health), nameof(PilaGenerationLine.Pension), nameof(PilaGenerationLine.SolidarityFund), nameof(PilaGenerationLine.SubsistenceFund), nameof(PilaGenerationLine.WorkRisk),
                                  nameof(PilaGenerationLine.FamilyCompensation), nameof(PilaGenerationLine.Sena), nameof(PilaGenerationLine.Icbf) })
            builder.Property(p).HasPrecision(18, 2);
        foreach (var p in new[] { nameof(PilaGenerationLine.HealthRate), nameof(PilaGenerationLine.PensionRate), nameof(PilaGenerationLine.WorkRiskRate), nameof(PilaGenerationLine.FamilyCompensationRate), nameof(PilaGenerationLine.SenaRate), nameof(PilaGenerationLine.IcbfRate) })
            builder.Property(p).HasPrecision(9, 7);

        builder.HasIndex(e => new { e.GenerationId, e.LineNumber }).IsUnique().HasDatabaseName("UK_PAY_PilaGenerationLines_Generation_Line");
        builder.HasIndex(e => new { e.GenerationId, e.EmployeeId }).HasDatabaseName("IX_PAY_PilaGenerationLines_Generation_Employee");
        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PilaIssueConfiguration : IEntityTypeConfiguration<PilaIssue>
{
    public void Configure(EntityTypeBuilder<PilaIssue> builder)
    {
        builder.ToTable("PAY_PilaIssues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_PAY_PilaIssues_PublicId");

        builder.Property(e => e.Code).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(500).IsRequired();
        builder.Property(e => e.LinkRoute).HasMaxLength(200);

        builder.HasIndex(e => new { e.GenerationId, e.Severity }).HasDatabaseName("IX_PAY_PilaIssues_Generation_Severity");
        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
