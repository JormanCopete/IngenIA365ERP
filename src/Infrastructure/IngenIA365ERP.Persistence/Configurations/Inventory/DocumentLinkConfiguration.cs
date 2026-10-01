using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentLinks</c> (feature 012, T17, T136; data-model §5.5). Único <c>(SourceDocumentId, TargetDocumentId,
/// Kind)</c> entre vivos (el descarte del borrador destino da de baja lógica sus vínculos) e índice por destino. Dos FK
/// <c>Restrict</c> a <c>INV_Documents</c>.
/// </summary>
public class DocumentLinkConfiguration : IEntityTypeConfiguration<DocumentLink>
{
    public void Configure(EntityTypeBuilder<DocumentLink> builder)
    {
        builder.ToTable("INV_DocumentLinks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentLinks_PublicId");

        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();

        builder.HasOne(e => e.SourceDocument).WithMany().HasForeignKey(e => e.SourceDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TargetDocument).WithMany().HasForeignKey(e => e.TargetDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.LineLinks).WithOne(l => l.DocumentLink).HasForeignKey(l => l.DocumentLinkId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SourceDocumentId, e.TargetDocumentId, e.Kind })
            .IsUnique()
            .HasDatabaseName("UK_INV_DocumentLinks_Source_Target_Kind")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.TargetDocumentId).HasDatabaseName("IX_INV_DocumentLinks_TargetDocumentId");

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
