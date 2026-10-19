using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Deposits;

// Feature 014, US9 (T151; data-model §7.1): las filas de preparación de la migración de aportes y captaciones. Cada una cuelga de su tanda
// (LND_MigrationLoadParts) con el único (tanda, fila); la llave natural de la parte la valida el comando entre tandas, no un índice (como la 013). Las
// personas y las líneas se nombran por FK; la oficina por su PublicId (como la 013). Migración CaptacionesAportes (regenerable hasta T225).

/// <summary><c>LND_MigrationLineEquivalenceRows</c> (parte <c>line-equivalences</c>).</summary>
public class MigrationLineEquivalenceRowConfiguration : IEntityTypeConfiguration<MigrationLineEquivalenceRow>
{
    public const string Tabla = "LND_MigrationLineEquivalenceRows";

    public void Configure(EntityTypeBuilder<MigrationLineEquivalenceRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceProductCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SourceDescription).HasMaxLength(120);
        builder.Property(e => e.CreditLineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Nature).HasConversion<int>().IsRequired();
        builder.Property(e => e.DepositClass).HasConversion<int?>();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationLineEquivalenceRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationContributionAccountRows</c> (parte <c>contribution-accounts</c>).</summary>
public class MigrationContributionAccountRowConfiguration : IEntityTypeConfiguration<MigrationContributionAccountRow>
{
    public const string Tabla = "LND_MigrationContributionAccountRows";

    public void Configure(EntityTypeBuilder<MigrationContributionAccountRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30);
        builder.Property(e => e.SourceProductCode).HasMaxLength(20);
        builder.Property(e => e.CreditLineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.OfficePublicId).IsRequired();
        builder.Property(e => e.OpenedOn).IsRequired();
        builder.Property(e => e.MinimumBalance).Monto().IsRequired();
        builder.Property(e => e.OrdinaryBalance).Monto().IsRequired();
        builder.Property(e => e.ExtraordinaryBalance).Monto().IsRequired();
        builder.Property(e => e.RevaluedBalance).Monto().IsRequired();
        builder.Property(e => e.MinimumNonReducible).Monto();
        builder.Property(e => e.RegisteredIncome).Monto();
        builder.Property(e => e.InstallmentAmount).Monto();
        builder.Ignore(e => e.Total);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationContributionAccountRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationAccountRows</c> (parte <c>member-accounts</c>).</summary>
public class MigrationAccountRowConfiguration : IEntityTypeConfiguration<MigrationAccountRow>
{
    public const string Tabla = "LND_MigrationAccountRows";

    public void Configure(EntityTypeBuilder<MigrationAccountRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.SourceProductCode).HasMaxLength(20);
        builder.Property(e => e.CreditLineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.OfficePublicId).IsRequired();
        builder.Property(e => e.DepositClass).HasConversion<int>().IsRequired();
        builder.Property(e => e.OpenedOn).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.HandlingMode).HasConversion<int>().IsRequired();
        builder.Property(e => e.Principal).Monto().IsRequired();
        builder.Property(e => e.InterestPayable).MontoCausado().IsRequired();
        builder.Property(e => e.ChargeReceivable).Monto().IsRequired();
        builder.Property(e => e.ClearingAmount).Monto().IsRequired();
        builder.Property(e => e.SourceOfFundsReference).HasMaxLength(200);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationAccountRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationAccountPartyRows</c> (parte <c>member-account-parties</c>).</summary>
public class MigrationAccountPartyRowConfiguration : IEntityTypeConfiguration<MigrationAccountPartyRow>
{
    public const string Tabla = "LND_MigrationAccountPartyRows";

    public void Configure(EntityTypeBuilder<MigrationAccountPartyRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Role).HasConversion<int>().IsRequired();
        builder.Property(e => e.Share).Fraccion();
        builder.Property(e => e.SignatureLimit).Monto();
        builder.Property(e => e.RepresentationBasis).HasMaxLength(40);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.SupportReference).HasMaxLength(200);

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationAccountPartyRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationAccountInstallmentRows</c> (parte <c>account-installments</c>, bigint).</summary>
public class MigrationAccountInstallmentRowConfiguration : IEntityTypeConfiguration<MigrationAccountInstallmentRow>
{
    public const string Tabla = "LND_MigrationAccountInstallmentRows";

    public void Configure(EntityTypeBuilder<MigrationAccountInstallmentRow> builder)
    {
        builder.ComoEntidadLargaDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PeriodYear).IsRequired();
        builder.Property(e => e.PeriodMonth).IsRequired();
        builder.Property(e => e.SubPeriodNumber).IsRequired();
        builder.Property(e => e.DueDate).IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.PaidAmount).Monto().IsRequired();
        builder.Property(e => e.NonDeductionReason).HasConversion<int?>();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationAccountInstallmentRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationGmfExemptionRows</c> (parte <c>gmf-exemptions</c>).</summary>
public class MigrationGmfExemptionRowConfiguration : IEntityTypeConfiguration<MigrationGmfExemptionRow>
{
    public const string Tabla = "LND_MigrationGmfExemptionRows";

    public void Configure(EntityTypeBuilder<MigrationGmfExemptionRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.MarkedSince).IsRequired();
        builder.Property(e => e.MonthExemptUsed).Monto().IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationGmfExemptionRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationTaxYearRows</c> (parte <c>tax-year-to-date</c>).</summary>
public class MigrationTaxYearRowConfiguration : IEntityTypeConfiguration<MigrationTaxYearRow>
{
    public const string Tabla = "LND_MigrationTaxYearRows";

    public void Configure(EntityTypeBuilder<MigrationTaxYearRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.TaxYear).IsRequired();
        builder.Property(e => e.Concept).HasConversion<int>().IsRequired();
        builder.Property(e => e.Amount).Monto().IsRequired();
        builder.Property(e => e.WithholdingBase).Monto();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationTaxYearRows_Part_Row");
    }
}

/// <summary><c>LND_MigrationTermDepositRows</c> (parte <c>term-deposits</c>, K9, US6, T169).</summary>
public class MigrationTermDepositRowConfiguration : IEntityTypeConfiguration<MigrationTermDepositRow>
{
    public const string Tabla = "LND_MigrationTermDepositRows";

    public void Configure(EntityTypeBuilder<MigrationTermDepositRow> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);
        builder.Property(e => e.RowNumber).IsRequired();
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.SourceProductCode).HasMaxLength(20);
        builder.Property(e => e.CreditLineCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.OfficePublicId).IsRequired();
        builder.Property(e => e.SourceTitleNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.PrincipalAmount).Monto().IsRequired();
        builder.Property(e => e.AppliedAnnualRate).TasaAnual().IsRequired();
        builder.Property(e => e.TableAnnualRate).TasaAnual();
        builder.Property(e => e.NegotiatedSpread).TasaAnual();
        builder.Property(e => e.InterestPeriodicity).HasConversion<int>().IsRequired();
        builder.Property(e => e.InterestTiming).HasConversion<int>().IsRequired();
        builder.Property(e => e.InterestPayable).MontoCausado().IsRequired();
        builder.Property(e => e.InterestDestination).HasConversion<int>().IsRequired();
        builder.Property(e => e.DestinationAccountNumber).HasMaxLength(30);
        builder.Property(e => e.MaturityInstruction).HasConversion<int>().IsRequired();
        builder.Property(e => e.IssueTermBand).HasConversion<int>().IsRequired();

        builder.HasOne<MigrationLoadPart>().WithMany().HasForeignKey(e => e.MigrationLoadPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditLine>().WithMany().HasForeignKey(e => e.CreditLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.MigrationLoadPartId, e.RowNumber }).IsUnique().HasDatabaseName("UK_LND_MigrationTermDepositRows_Part_Row");
    }
}
