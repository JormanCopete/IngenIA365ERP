using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentSequences</c> (feature 012, T16, FR-038, T136; data-model §5.9). Único <c>(DocumentTypeId, Prefix)</c>
/// entre vivos: el número es único por tipo y prefijo, así que volver a un prefijo reabre su fila. <c>NextValue</c> es
/// <c>bigint</c> y sólo lo escribe <c>Numerador</c>.
/// </summary>
public class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("INV_DocumentSequences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentSequences_PublicId");

        builder.Property(e => e.Prefix).HasMaxLength(10).IsRequired();
        builder.Property(e => e.NextValue).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);

        builder.HasIndex(e => new { e.DocumentTypeId, e.Prefix })
            .IsUnique()
            .HasDatabaseName("UK_INV_DocumentSequences_Type_Prefix")
            .HasFilter("[IsDeleted] = 0");

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
