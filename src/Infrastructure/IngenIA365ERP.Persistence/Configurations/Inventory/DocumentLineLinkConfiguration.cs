using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentLineLinks</c> (feature 012, T17, T136; data-model §5.5). Único <c>(SourceLineId, TargetLineId)</c>
/// entre vivos e índice por línea destino; cantidad en unidad base (18,4). FK <c>Restrict</c> al vínculo y a las dos
/// líneas.
/// </summary>
public class DocumentLineLinkConfiguration : IEntityTypeConfiguration<DocumentLineLink>
{
    public void Configure(EntityTypeBuilder<DocumentLineLink> builder)
    {
        builder.ToTable("INV_DocumentLineLinks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentLineLinks_PublicId");

        builder.Property(e => e.QuantityBase).Cantidad().IsRequired();

        builder.HasOne(e => e.SourceLine).WithMany().HasForeignKey(e => e.SourceLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TargetLine).WithMany().HasForeignKey(e => e.TargetLineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SourceLineId, e.TargetLineId })
            .IsUnique()
            .HasDatabaseName("UK_INV_DocumentLineLinks_SourceLine_TargetLine")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.TargetLineId).HasDatabaseName("IX_INV_DocumentLineLinks_TargetLineId");

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
