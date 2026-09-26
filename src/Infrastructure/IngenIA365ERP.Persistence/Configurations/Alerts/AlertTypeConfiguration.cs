using IngenIA365ERP.Domain.Entities.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Alerts;

/// <summary>
/// <c>COR_AlertTypes</c> (feature 012, T39, T091; data-model §22). Único <c>(TypeCode, ValidFrom)</c> filtrado a filas
/// vivas; <c>Channels</c> y <c>Severity</c> como int. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class AlertTypeConfiguration : IEntityTypeConfiguration<AlertType>
{
    public void Configure(EntityTypeBuilder<AlertType> builder)
    {
        builder.ToTable("COR_AlertTypes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_AlertTypes_PublicId");

        builder.Property(e => e.TypeCode).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Module).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RecipientPermissions).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Channels).HasConversion<int>().IsRequired();
        builder.Property(e => e.Severity).HasConversion<int>().IsRequired();
        builder.Property(e => e.ThresholdsJson).HasMaxLength(2000);
        builder.Property(e => e.IsEnabled).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => new { e.TypeCode, e.ValidFrom })
            .IsUnique()
            .HasDatabaseName("UK_COR_AlertTypes_TypeCode_ValidFrom")
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
