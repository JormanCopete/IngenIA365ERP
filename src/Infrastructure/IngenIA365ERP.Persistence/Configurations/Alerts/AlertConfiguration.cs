using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Alerts;

/// <summary>
/// <c>COR_Alerts</c> (feature 012, T39, T091; data-model §22). Una sola pendiente por condición: único
/// <c>(DedupKey)</c> filtrado <c>[Status] = 0 AND [IsDeleted] = 0</c>; la bandeja busca por
/// <c>(Status, TypeCode, RaisedAt)</c>. Enumeraciones como int. FK <c>Restrict</c> a la versión del tipo y a
/// <c>SEC_Users</c> (quien la atendió). La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("COR_Alerts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Alerts_PublicId");

        builder.Property(e => e.TypeCode).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Severity).HasConversion<int>().IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Body).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(60);
        builder.Property(e => e.EntityPublicId);
        builder.Property(e => e.ScopeWarehousePublicId);
        builder.Property(e => e.ScopePointOfSalePublicId);
        builder.Property(e => e.DedupKey).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RaisedAt).IsRequired();
        builder.Property(e => e.RaisedByKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.RaisedByName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.OccurrenceCount).IsRequired();
        builder.Property(e => e.LastOccurredAt).IsRequired();
        builder.Property(e => e.AttendedAt);
        builder.Property(e => e.AttendedByKind).HasConversion<int?>();
        builder.Property(e => e.AttendedByName).HasMaxLength(150);
        builder.Property(e => e.AttendNote).HasMaxLength(1000);
        builder.Property(e => e.RecipientCount).IsRequired();
        builder.Property(e => e.WithoutRecipient).IsRequired();

        builder.HasOne(e => e.AlertType).WithMany().HasForeignKey(e => e.AlertTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AttendedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DedupKey)
            .IsUnique()
            .HasDatabaseName("UK_COR_Alerts_DedupKey_Pending")
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Status, e.TypeCode, e.RaisedAt })
            .HasDatabaseName("IX_COR_Alerts_Status_TypeCode_RaisedAt");

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
