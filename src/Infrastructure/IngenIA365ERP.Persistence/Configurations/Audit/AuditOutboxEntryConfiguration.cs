using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Audit;

/// <summary>
/// <c>COR_AuditOutbox</c> (feature 012, T061; data-model §23, §24). Únicos: <c>EventId</c> y
/// <c>(Stream, Seq)</c> filtrado a lo sellado. El índice de pendientes es por <c>Id</c> filtrado a
/// <c>[Forwarded] = 0</c>: el reenviador lee por <c>Id</c> y nunca desde un «último Id», porque una
/// transacción puede confirmar tarde. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class AuditOutboxEntryConfiguration : IEntityTypeConfiguration<AuditOutboxEntry>
{
    public void Configure(EntityTypeBuilder<AuditOutboxEntry> builder)
    {
        builder.ToTable("COR_AuditOutbox");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_AuditOutbox_PublicId");

        builder.Property(e => e.EventId).IsRequired();
        builder.HasIndex(e => e.EventId).IsUnique().HasDatabaseName("UK_COR_AuditOutbox_EventId");

        builder.Property(e => e.Stream).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(30).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.PayloadJson);
        builder.Property(e => e.Forwarded).IsRequired();
        builder.Property(e => e.ForwardedAt);
        builder.Property(e => e.ForwardAttempts).IsRequired();
        builder.Property(e => e.LastForwardError).HasMaxLength(500);
        builder.Property(e => e.Seq);
        builder.Property(e => e.PrevHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.Property(e => e.Hash).HasMaxLength(64).IsFixedLength().IsUnicode(false);

        builder.HasIndex(e => new { e.Stream, e.Seq }).IsUnique()
            .HasDatabaseName("UK_COR_AuditOutbox_Stream_Seq").HasFilter("[Seq] IS NOT NULL");
        builder.HasIndex(e => e.Id)
            .HasDatabaseName("IX_COR_AuditOutbox_Pending").HasFilter("[Forwarded] = 0");

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
