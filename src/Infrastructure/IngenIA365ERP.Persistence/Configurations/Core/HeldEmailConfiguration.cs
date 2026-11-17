using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary><c>COR_HeldEmails</c> (2026-11-17): los correos que el modo de prueba suspendió o desvió. (nuevo)</summary>
public class HeldEmailConfiguration : IEntityTypeConfiguration<HeldEmail>
{
    public void Configure(EntityTypeBuilder<HeldEmail> builder)
    {
        builder.ToTable("COR_HeldEmails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_HeldEmails_PublicId");

        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("IX_COR_HeldEmails_Status_CreatedAt");

        builder.Property(e => e.OriginalTo).HasMaxLength(320).IsRequired();
        builder.Property(e => e.RedirectedTo).HasMaxLength(1000);
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.BodyHtml);
        builder.Property(e => e.BodyText);
        builder.Property(e => e.FromOverride).HasMaxLength(320);
        builder.Property(e => e.ResentBy).HasMaxLength(150);

        builder.HasMany(e => e.Attachments).WithOne().HasForeignKey(a => a.HeldEmailId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary><c>COR_HeldEmailAttachments</c>: los adjuntos de un correo pendiente de reenviar. (nuevo)</summary>
public class HeldEmailAttachmentConfiguration : IEntityTypeConfiguration<HeldEmailAttachment>
{
    public void Configure(EntityTypeBuilder<HeldEmailAttachment> builder)
    {
        builder.ToTable("COR_HeldEmailAttachments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_HeldEmailAttachments_PublicId");
        builder.HasIndex(e => e.HeldEmailId).HasDatabaseName("IX_COR_HeldEmailAttachments_HeldEmailId");

        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Content).IsRequired();

        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
