using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_AssociateInboundMessages</c> (feature 014, US10, T098; contracts/mensajes.md §8): el recibo de Asociados de los mensajes de la Cartera.
/// <c>UK_COR_AssociateInboundMessages_Message</c> sin filtro (recibo: una colisión es <c>AlreadyProcessed</c>) e índice por asociado. Migración
/// <c>CaptacionesAportes</c>. (nuevo)
/// </summary>
public class AssociateInboundMessageConfiguration : IEntityTypeConfiguration<AssociateInboundMessage>
{
    public void Configure(EntityTypeBuilder<AssociateInboundMessage> builder)
    {
        builder.ToTable("COR_AssociateInboundMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_AssociateInboundMessages_PublicId");

        builder.Property(e => e.MessageType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.MessageVersion).IsRequired();
        builder.Property(e => e.WithdrawalNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Cause).HasMaxLength(30).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();

        builder.HasOne<Associate>().WithMany().HasForeignKey(e => e.AssociateId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MessagePublicId).IsUnique().HasDatabaseName("UK_COR_AssociateInboundMessages_Message");
        builder.HasIndex(e => e.AssociateId).HasDatabaseName("IX_COR_AssociateInboundMessages_Associate");

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
