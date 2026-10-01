using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Audit;

/// <summary>
/// <c>COR_AuditAnchors</c> (feature 012, T061; data-model §23, §24). Únicos <c>(Stream, Kind, Seq)</c> y
/// <c>(Stream, AnchorDate)</c> filtrado a las diarias (<c>[Kind] = 3</c>, <see cref="Domain.Enums.Audit.AuditAnchorKind.Daily"/>):
/// una sola ancla diaria por día aunque dos réplicas lo intenten.
/// </summary>
public class AuditAnchorConfiguration : IEntityTypeConfiguration<AuditAnchor>
{
    public void Configure(EntityTypeBuilder<AuditAnchor> builder)
    {
        builder.ToTable("COR_AuditAnchors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_AuditAnchors_PublicId");

        builder.Property(e => e.Stream).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Seq).IsRequired();
        builder.Property(e => e.Hash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.AnchorDate);
        builder.Property(e => e.Hmac).HasMaxLength(128).IsRequired();
        builder.Property(e => e.KeyVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AnchoredAt).IsRequired();

        builder.HasIndex(e => new { e.Stream, e.Kind, e.Seq }).IsUnique()
            .HasDatabaseName("UK_COR_AuditAnchors_Stream_Kind_Seq");
        builder.HasIndex(e => new { e.Stream, e.AnchorDate }).IsUnique()
            .HasDatabaseName("UK_COR_AuditAnchors_Stream_AnchorDate").HasFilter("[Kind] = 3");

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
