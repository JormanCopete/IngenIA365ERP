using IngenIA365ERP.Domain.Entities.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Integration;

/// <summary>
/// <c>COR_IntegrationMessageDeliveries</c> (feature 012, T7, T9, T074; data-model §19). Una fila por mensaje y
/// destino (único <c>(MessageId, Destination)</c>). Los dos índices filtrados son las dos colas: las
/// elegibles (<c>[Status] = 0</c>, <c>Pending</c>, por destino y hora del próximo intento) y las que esperan
/// lote (<c>[Status] = 1</c>, <c>InBatch</c>, por clave de horario). <c>BatchId</c> va <b>sin FK</b>: la FK entra
/// con <c>COR_IntegrationBatches</c> en I2. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class IntegrationMessageDeliveryConfiguration : IEntityTypeConfiguration<IntegrationMessageDelivery>
{
    public void Configure(EntityTypeBuilder<IntegrationMessageDelivery> builder)
    {
        builder.ToTable("COR_IntegrationMessageDeliveries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationMessageDeliveries_PublicId");

        builder.HasOne(e => e.Message).WithMany().HasForeignKey(e => e.MessageId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Destination).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ScheduleKey).HasMaxLength(60);
        builder.Property(e => e.BatchScopeKey).HasMaxLength(60);
        builder.Property(e => e.BatchId);
        builder.Property(e => e.Attempts).IsRequired();
        builder.Property(e => e.NextAttemptAt);
        builder.Property(e => e.LastAttemptAt);
        builder.Property(e => e.LastErrorCode).HasMaxLength(80);
        builder.Property(e => e.LastErrorMessage).HasMaxLength(1000);
        builder.Property(e => e.LastErrorDataJson);
        builder.Property(e => e.ProcessedAt);
        builder.Property(e => e.ResultReference).HasMaxLength(100);
        builder.Property(e => e.ResultVoucherTypeCode).HasMaxLength(10);
        builder.Property(e => e.ResultVoucherNumber).HasMaxLength(30);

        builder.HasIndex(e => new { e.MessageId, e.Destination }).IsUnique()
            .HasDatabaseName("UK_COR_IntegrationMessageDeliveries_Message_Destination");
        builder.HasIndex(e => new { e.Destination, e.NextAttemptAt, e.MessageId })
            .HasDatabaseName("IX_COR_IntegrationMessageDeliveries_Eligible").HasFilter("[Status] = 0");
        builder.HasIndex(e => new { e.ScheduleKey, e.MessageId })
            .HasDatabaseName("IX_COR_IntegrationMessageDeliveries_InBatch").HasFilter("[Status] = 1");
        builder.HasIndex(e => e.BatchId).HasDatabaseName("IX_COR_IntegrationMessageDeliveries_Batch");

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
