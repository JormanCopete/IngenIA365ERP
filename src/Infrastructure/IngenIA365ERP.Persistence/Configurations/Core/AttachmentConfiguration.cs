using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("COR_Attachments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Attachments_PublicId");

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.OwnerEntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.OwnerEntityPublicId).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SizeBytes).IsRequired();
        builder.Property(e => e.Sha256Hex).HasMaxLength(64).IsRequired();
        builder.Property(e => e.StoragePath).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.StorageProvider).HasMaxLength(50).IsRequired().HasDefaultValue("Local");
        builder.Property(e => e.EncryptedDek).HasMaxLength(1000).IsRequired();

        // Índice combinado por owner — el patrón de consulta es "lista los
        // adjuntos del User X" → (TenantId, OwnerEntityType, OwnerEntityPublicId).
        builder.HasIndex(e => new { e.TenantId, e.OwnerEntityType, e.OwnerEntityPublicId })
            .HasDatabaseName("IX_COR_Attachments_Owner");

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
