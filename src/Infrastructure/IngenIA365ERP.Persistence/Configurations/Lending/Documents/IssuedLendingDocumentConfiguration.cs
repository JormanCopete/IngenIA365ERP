using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocuments</c> (feature 013, C.3; data-model §27.1): hecho. Únicos <c>Number</c> y <c>VerificationCode</c> (16), sin
/// filtro; huellas de 64.
/// </summary>
public class IssuedLendingDocumentConfiguration : IEntityTypeConfiguration<IssuedLendingDocument>
{
    public const string Tabla = "LND_IssuedDocuments";

    public void Configure(EntityTypeBuilder<IssuedLendingDocument> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Number).HasMaxLength(30).IsRequired();
        builder.Property(e => e.HolderTaxIdType).HasMaxLength(10).IsRequired();
        builder.Property(e => e.HolderTaxId).HasMaxLength(30).IsRequired();
        builder.Property(e => e.HolderName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.InformationCutoff).IsRequired();
        builder.Property(e => e.VerificationCode).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(EntidadDeCartera.Huella).IsRequired();
        builder.Property(e => e.PdfAttachmentPublicId).IsRequired();
        builder.Property(e => e.EssentialDataJson).IsRequired();
        builder.Property(e => e.SettlementSha256).HasMaxLength(EntidadDeCartera.Huella);
        builder.Property(e => e.RulesVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.OutsideLendingNote).HasMaxLength(1000);
        builder.Property(e => e.SignerName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.SignerTitle).HasMaxLength(150).IsRequired();
        builder.Property(e => e.IsTestEnvironment).IsRequired();
        builder.Property(e => e.IssuedAt).IsRequired();
        builder.Property(e => e.DeliveryMode).HasConversion<int>().IsRequired();

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_LND_IssuedDocuments_Number");
        builder.HasIndex(e => e.VerificationCode).IsUnique().HasDatabaseName("UK_LND_IssuedDocuments_VerificationCode");
        builder.HasIndex(e => e.PersonId).HasDatabaseName("IX_LND_IssuedDocuments_PersonId");
    }
}
