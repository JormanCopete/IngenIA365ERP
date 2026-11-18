using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Documents;

/// <summary><c>LND_SignatureDocumentTemplates</c> (2026-11-18): el código es único entre las plantillas vivas.</summary>
public class SignatureDocumentTemplateConfiguration : IEntityTypeConfiguration<SignatureDocumentTemplate>
{
    public const string Tabla = "LND_SignatureDocumentTemplates";

    public void Configure(EntityTypeBuilder<SignatureDocumentTemplate> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Code).HasMaxLength(SignatureDocumentTemplate.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(SignatureDocumentTemplate.LargoDelNombre).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.RepaymentChannels).HasMaxLength(200);
        builder.Property(e => e.CreditLineCodes).HasMaxLength(2000);
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.PrintOrder).IsRequired();
        builder.Property(e => e.IncludeFingerprint).IsRequired();
        builder.Property(e => e.SignedByCoDebtors).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.IsVerified).IsRequired();
        builder.Property(e => e.VerificationNote).HasMaxLength(SignatureDocumentTemplate.LargoDeLaNota);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_SignatureDocumentTemplates_Code").HasFilter("[IsDeleted] = 0");
    }
}

/// <summary><c>LND_SignatureDocumentFields</c> (2026-11-18): el código es único entre los campos vivos.</summary>
public class SignatureDocumentFieldConfiguration : IEntityTypeConfiguration<SignatureDocumentField>
{
    public const string Tabla = "LND_SignatureDocumentFields";

    public void Configure(EntityTypeBuilder<SignatureDocumentField> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(SignatureDocumentField.LargoDelCodigo).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(SignatureDocumentField.LargoDelNombre).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Source).HasConversion<int>().IsRequired();
        builder.Property(e => e.ConstantValue).HasMaxLength(SignatureDocumentField.LargoDelValor);
        builder.Property(e => e.DefaultValue).HasMaxLength(SignatureDocumentField.LargoDelValor);
        builder.Property(e => e.IsRequired).IsRequired();
        builder.Property(e => e.HelpText).HasMaxLength(SignatureDocumentField.LargoDeLaAyuda);
        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_SignatureDocumentFields_Code").HasFilter("[IsDeleted] = 0");
    }
}

/// <summary><c>LND_SignatureDocumentSets</c> (2026-11-18): los juegos de una solicitud.</summary>
public class SignatureDocumentSetConfiguration : IEntityTypeConfiguration<SignatureDocumentSet>
{
    public const string Tabla = "LND_SignatureDocumentSets";

    public void Configure(EntityTypeBuilder<SignatureDocumentSet> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.ApplicationId).IsRequired();
        builder.Property(e => e.PromissoryNotePublicId);
        builder.Property(e => e.GeneratedAt).IsRequired();
        builder.Property(e => e.AttachmentPublicId).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.DocumentsJson).IsRequired();
        builder.Property(e => e.FieldsJson).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ApplicationId, e.Status }).HasDatabaseName("IX_LND_SignatureDocumentSets_ApplicationId_Status");
    }
}
