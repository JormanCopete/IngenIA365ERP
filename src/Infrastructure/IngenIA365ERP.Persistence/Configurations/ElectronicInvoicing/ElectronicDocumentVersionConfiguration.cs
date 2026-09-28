using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_ElectronicDocumentVersions</c> (feature 012, I4, T698; data-model §18): hecho inmutable, único
/// <c>(ElectronicDocumentId, VersionNumber)</c>. <c>CanonicalSha256</c> y <c>EconomicFingerprint</c> son <c>char(64)</c>;
/// las cuatro referencias a artefactos son de escritura única (las respeta la guarda de <c>IHechoInmutable</c>). La FK al
/// documento la declara <c>ElectronicDocumentConfiguration</c>. Llega con la migración <c>DocumentosElectronicos</c>.
/// </summary>
public class ElectronicDocumentVersionConfiguration : IEntityTypeConfiguration<ElectronicDocumentVersion>
{
    public const string Tabla = "COR_ElectronicDocumentVersions";

    public void Configure(EntityTypeBuilder<ElectronicDocumentVersion> builder)
    {
        builder.ComoEntidadDeFacturacionElectronica(Tabla);

        builder.Property(e => e.ElectronicDocumentId).IsRequired();
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.SourceDocumentPublicId).IsRequired();
        builder.Property(e => e.Reason).HasConversion<int>().IsRequired();
        builder.Property(e => e.CanonicalSchemaVersion).IsRequired();
        builder.Property(e => e.CanonicalSha256).Sha256().IsRequired();
        builder.Property(e => e.EconomicFingerprint).Sha256().IsRequired();
        builder.Property(e => e.CorrectionReason).HasMaxLength(500);
        builder.Property(e => e.ChangedFieldsJson);
        builder.Property(e => e.CanonicalAttachmentPublicId);
        builder.Property(e => e.SignedXmlAttachmentPublicId);
        builder.Property(e => e.AttachedDocumentAttachmentPublicId);
        builder.Property(e => e.GraphicPdfAttachmentPublicId);

        builder.HasIndex(e => new { e.ElectronicDocumentId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("UK_COR_ElectronicDocumentVersions_Document_Version");
    }
}
