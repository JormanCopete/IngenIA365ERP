using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

// Feature 015, R2 (fase 38, T0433; par RecaudoCanales; data-model §8.2; DT §2.3): los convenios de recaudo, las referencias de pago, los archivos de convenio y de
// pasarela y su conciliación. FK Restrict a la plataforma y entre tablas RCD_; la cuenta recaudadora es de Tesorería y se guarda por su PublicId (R-05).

/// <summary><c>RCD_CollectionAgreements</c>: código único; FK a la oficina y a la persona del aliado. (nuevo)</summary>
public class CollectionAgreementConfiguration : IEntityTypeConfiguration<CollectionAgreement>
{
    public const string Tabla = "RCD_CollectionAgreements";

    public void Configure(EntityTypeBuilder<CollectionAgreement> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Branch>().WithMany().HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PartnerPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.PartnerAgreementCode).HasMaxLength(40);
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName($"UK_{Tabla}_Code");
    }
}

/// <summary><c>RCD_CollectionAgreementVersions</c>: por convenio y vigencia; FK al formato del archivo. (nuevo)</summary>
public class CollectionAgreementVersionConfiguration : IEntityTypeConfiguration<CollectionAgreementVersion>
{
    public const string Tabla = "RCD_CollectionAgreementVersions";

    public void Configure(EntityTypeBuilder<CollectionAgreementVersion> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionAgreement>().WithMany().HasForeignKey(e => e.CollectionAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankFileFormat>().WithMany().HasForeignKey(e => e.BankFileFormatId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.ReferenceMode).HasConversion<int>();
        builder.Property(e => e.CheckDigit).HasConversion<int>();
        builder.Property(e => e.CommissionPerPayment).Monto();
        builder.Property(e => e.CommissionRate).Tarifa();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.CollectionAgreementId, e.ValidFrom }).IsUnique().HasDatabaseName($"UK_{Tabla}_Agreement_ValidFrom");
    }
}

/// <summary><c>RCD_PaymentReferences</c>: la referencia es única (nunca se reutiliza); por producto y por persona. (nuevo)</summary>
public class PaymentReferenceConfiguration : IEntityTypeConfiguration<PaymentReference>
{
    public const string Tabla = "RCD_PaymentReferences";

    public void Configure(EntityTypeBuilder<PaymentReference> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Reference).HasMaxLength(40).IsRequired();
        builder.Property(e => e.BaseNumber).HasMaxLength(40).IsRequired();
        builder.Property(e => e.CheckDigitValue).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Algorithm).HasConversion<int>();
        builder.Property(e => e.Mode).HasConversion<int>();
        builder.Property(e => e.Target).HasConversion<int?>();
        builder.Property(e => e.ProductNumber).HasMaxLength(40);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.Reference).IsUnique().HasDatabaseName($"UK_{Tabla}_Reference");
        builder.HasIndex(e => e.ProductPublicId).HasDatabaseName($"IX_{Tabla}_Product");
        builder.HasIndex(e => new { e.PersonId, e.Mode }).HasDatabaseName($"IX_{Tabla}_Person_Mode");
    }
}

/// <summary>Lo común de la configuración de un archivo de convenio o de pasarela y de sus líneas. (nuevo)</summary>
internal static class ArchivoDeRecaudoConfiguracion
{
    public static void Configurar<T>(EntityTypeBuilder<T> builder, string tabla) where T : CollectionFileBase
    {
        builder.ComoEntidadDeRecaudo(tabla);
        builder.HasOne<CollectionAgreement>().WithMany().HasForeignKey(e => e.CollectionAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CollectionAgreementVersion>().WithMany().HasForeignKey(e => e.CollectionAgreementVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.LoadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProcessingRun>().WithMany().HasForeignKey(e => e.ProcessingRunId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(200);
        builder.Property(e => e.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.TotalInformed).Monto();
        builder.Property(e => e.TotalApplied).Monto();
        builder.Property(e => e.TotalUnidentified).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{tabla}_Number");
        builder.HasIndex(e => new { e.CollectionAgreementId, e.FileSha256 }).IsUnique().HasDatabaseName($"UK_{tabla}_Agreement_Sha256");
    }

    public static void ConfigurarLinea<T>(EntityTypeBuilder<T> builder, string tabla) where T : CollectionFileLineBase
    {
        builder.ComoEntidadDeRecaudo(tabla);
        builder.HasOne<Receipt>().WithMany().HasForeignKey(e => e.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UnidentifiedItem>().WithMany().HasForeignKey(e => e.UnidentifiedItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentReference>().WithMany().HasForeignKey(e => e.PaymentReferenceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Amount).Monto();
        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.PartnerSequence).HasMaxLength(60);
        builder.Property(e => e.PayerTaxId).HasMaxLength(20);
        builder.Property(e => e.PayerName).HasMaxLength(200);
        builder.Property(e => e.Outcome).HasConversion<int>();
        builder.Property(e => e.Code).HasMaxLength(100);
        builder.Property(e => e.Message).HasMaxLength(1000);
    }
}

/// <summary><c>RCD_AgreementFiles</c>: número único; la huella es única por convenio. (nuevo)</summary>
public class AgreementFileConfiguration : IEntityTypeConfiguration<AgreementFile>
{
    public const string Tabla = "RCD_AgreementFiles";

    public void Configure(EntityTypeBuilder<AgreementFile> builder) => ArchivoDeRecaudoConfiguracion.Configurar(builder, Tabla);
}

/// <summary><c>RCD_AgreementFileLines</c>: por archivo y línea; el identificador del pago del aliado, para no aplicarlo dos veces. (nuevo)</summary>
public class AgreementFileLineConfiguration : IEntityTypeConfiguration<AgreementFileLine>
{
    public const string Tabla = "RCD_AgreementFileLines";

    public void Configure(EntityTypeBuilder<AgreementFileLine> builder)
    {
        ArchivoDeRecaudoConfiguracion.ConfigurarLinea(builder, Tabla);
        builder.HasOne<AgreementFile>().WithMany().HasForeignKey(e => e.AgreementFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.AgreementFileId, e.LineNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_File_Line");
        builder.HasIndex(e => e.PartnerSequence).HasDatabaseName($"IX_{Tabla}_PartnerSequence");
    }
}

/// <summary><c>RCD_GatewayFiles</c>: número único; la huella es única por convenio. (nuevo)</summary>
public class GatewayFileConfiguration : IEntityTypeConfiguration<GatewayFile>
{
    public const string Tabla = "RCD_GatewayFiles";

    public void Configure(EntityTypeBuilder<GatewayFile> builder) => ArchivoDeRecaudoConfiguracion.Configurar(builder, Tabla);
}

/// <summary><c>RCD_GatewayFileLines</c>: por archivo y línea, con el resultado de la pasarela. (nuevo)</summary>
public class GatewayFileLineConfiguration : IEntityTypeConfiguration<GatewayFileLine>
{
    public const string Tabla = "RCD_GatewayFileLines";

    public void Configure(EntityTypeBuilder<GatewayFileLine> builder)
    {
        ArchivoDeRecaudoConfiguracion.ConfigurarLinea(builder, Tabla);
        builder.HasOne<GatewayFile>().WithMany().HasForeignKey(e => e.GatewayFileId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.GatewayOutcome).HasMaxLength(40);
        builder.Property(e => e.GatewayReference).HasMaxLength(60);
        builder.HasIndex(e => new { e.GatewayFileId, e.LineNumber }).IsUnique().HasDatabaseName($"UK_{Tabla}_File_Line");
        builder.HasIndex(e => e.PartnerSequence).HasDatabaseName($"IX_{Tabla}_PartnerSequence");
    }
}

/// <summary><c>RCD_AgreementReconciliations</c>: número único; una conciliación por abono. (nuevo)</summary>
public class AgreementReconciliationConfiguration : IEntityTypeConfiguration<AgreementReconciliation>
{
    public const string Tabla = "RCD_AgreementReconciliations";

    public void Configure(EntityTypeBuilder<AgreementReconciliation> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<CollectionAgreement>().WithMany().HasForeignKey(e => e.CollectionAgreementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AgreementFile>().WithMany().HasForeignKey(e => e.AgreementFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GatewayFile>().WithMany().HasForeignKey(e => e.GatewayFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.InformedAmount).Monto();
        builder.Property(e => e.CreditedAmount).Monto();
        builder.Property(e => e.CommissionAmount).Monto();
        builder.Property(e => e.VatAmount).Monto();
        builder.Property(e => e.WithholdingsAmount).Monto();
        builder.Property(e => e.UnexplainedAmount).Monto();
        builder.Property(e => e.Tolerance).Monto();
        builder.Property(e => e.WithholdingsJson).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName($"UK_{Tabla}_Number");
        builder.HasIndex(e => e.StatementLinePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_StatementLine");
    }
}
