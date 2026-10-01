using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_ElectronicDocumentTransmissions</c> (feature 012, I4, T698; data-model §18): un intento contra el canal, hecho
/// inmutable de identidad <c>bigint</c>. Único <c>(ElectronicDocumentId, AttemptNumber)</c>; FK <c>Restrict</c> a la
/// versión enviada y a <c>SEC_Users</c> (quién lo pidió). La FK al documento la declara
/// <c>ElectronicDocumentConfiguration</c>. Llega con la migración <c>DocumentosElectronicos</c>.
/// </summary>
public class ElectronicDocumentTransmissionConfiguration : IEntityTypeConfiguration<ElectronicDocumentTransmission>
{
    public const string Tabla = "COR_ElectronicDocumentTransmissions";

    public void Configure(EntityTypeBuilder<ElectronicDocumentTransmission> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");

        builder.Property(e => e.ElectronicDocumentId).IsRequired();
        builder.Property(e => e.VersionId).IsRequired();
        builder.Property(e => e.AttemptNumber).IsRequired();
        builder.Property(e => e.Operation).HasConversion<int>().IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.CompletedAt).IsRequired();
        builder.Property(e => e.DurationMs).IsRequired();
        builder.Property(e => e.RequestedByKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedByName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
        builder.Property(e => e.RequestSha256).Sha256().IsRequired();
        builder.Property(e => e.HttpStatus);
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.ProviderCode).HasMaxLength(20);
        builder.Property(e => e.DianStatusCode).HasMaxLength(10);
        builder.Property(e => e.IsValid);
        builder.Property(e => e.RawMessagesJson);
        builder.Property(e => e.TranslatedMessagesJson);
        builder.Property(e => e.ExternalReference).HasMaxLength(100);
        builder.Property(e => e.ApplicationResponseAttachmentPublicId);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);

        builder.HasOne(e => e.Version).WithMany().HasForeignKey(e => e.VersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ElectronicDocumentId, e.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UK_COR_ElectronicDocumentTransmissions_Document_Attempt");
        builder.HasIndex(e => e.VersionId).HasDatabaseName("IX_COR_ElectronicDocumentTransmissions_VersionId");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
