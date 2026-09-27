using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Integration;

// Feature 012, entrega I2 (T480; data-model §19; decisiones-transversales §2.2): los intentos de entrega, los lotes y su
// consecutivo. Las tres tablas llegan con la migración IntegracionContableDeInventario (T486). La FK de
// COR_IntegrationMessageDeliveries.BatchId, que nació en I1 sin llave, la pone IntegrationMessageDeliveryConfiguration.
// Los filtros se escriben con corchetes y ProviderModelConventions los traduce para PostgreSQL (comillas y FALSE).

/// <summary>
/// <c>COR_IntegrationDeliveryAttempts</c>: único <c>(DeliveryId, AttemptNumber)</c>; llaves <c>Restrict</c> a la entrega,
/// al usuario que actuó y al lote. Es un hecho: nada se borra en cascada.
/// </summary>
public class IntegrationDeliveryAttemptConfiguration : IEntityTypeConfiguration<IntegrationDeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<IntegrationDeliveryAttempt> builder)
    {
        builder.ToTable("COR_IntegrationDeliveryAttempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationDeliveryAttempts_PublicId");

        builder.HasOne(e => e.Delivery).WithMany().HasForeignKey(e => e.DeliveryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IntegrationBatch>().WithMany().HasForeignKey(e => e.BatchId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.MessageId).IsRequired();
        builder.Property(e => e.AttemptNumber).IsRequired();
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.FinishedAt).IsRequired();
        builder.Property(e => e.DurationMs).IsRequired();
        builder.Property(e => e.Outcome).HasConversion<int>().IsRequired();
        builder.Property(e => e.ErrorCode).HasMaxLength(80);
        builder.Property(e => e.ErrorMessage).HasMaxLength(1000);
        builder.Property(e => e.ActorKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ActorName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Instance).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => new { e.DeliveryId, e.AttemptNumber }).IsUnique()
            .HasDatabaseName("UK_COR_IntegrationDeliveryAttempts_Delivery_Attempt");
        builder.HasIndex(e => e.MessageId).HasDatabaseName("IX_COR_IntegrationDeliveryAttempts_Message");
        builder.HasIndex(e => e.BatchId).HasDatabaseName("IX_COR_IntegrationDeliveryAttempts_Batch");
        builder.HasIndex(e => e.ActorUserId).HasDatabaseName("IX_COR_IntegrationDeliveryAttempts_ActorUser");

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

/// <summary>
/// <c>COR_IntegrationBatches</c>: único <c>Number</c>; <c>UK_COR_IntegrationBatches_Schedule (ScheduleKey, ScheduledFor)</c>
/// filtrado a <c>Trigger = 1</c> (<c>Scheduled</c>) vivos, para que dos réplicas no creen la misma franja; y un lote por
/// cierre de sesión de caja, <c>(CashSessionPublicId)</c> filtrado a <c>Trigger = 2</c> vivos. <c>CutoffMessageId</c> es el
/// <c>Id</c> interno del mensaje de corte y va sin llave: el mensaje no se borra y el lote sólo lo usa como tope.
/// </summary>
public class IntegrationBatchConfiguration : IEntityTypeConfiguration<IntegrationBatch>
{
    /// <summary>El único de la franja programada: la colisión se traduce a «ya existe» (T466).</summary>
    public const string UnicoDeLaFranja = "UK_COR_IntegrationBatches_Schedule";

    /// <summary>El único del lote de cierre de una sesión de caja.</summary>
    public const string UnicoDeLaSesionDeCaja = "UK_COR_IntegrationBatches_CashSession";

    public void Configure(EntityTypeBuilder<IntegrationBatch> builder)
    {
        builder.ToTable("COR_IntegrationBatches");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationBatches_PublicId");

        builder.Property(e => e.Number).IsRequired();
        builder.Property(e => e.Destination).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Trigger).HasConversion<int>().IsRequired();
        builder.Property(e => e.ScheduleKey).HasMaxLength(60);
        builder.Property(e => e.ScheduledFor);
        builder.Property(e => e.CashSessionPublicId);
        builder.Property(e => e.PeriodYear);
        builder.Property(e => e.PeriodMonth);
        builder.Property(e => e.CutoffMessageId);
        builder.Property(e => e.DateFrom);
        builder.Property(e => e.DateTo);
        builder.Property(e => e.Granularity).HasConversion<int?>();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedByKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.RequestedByCentralUserId);
        builder.Property(e => e.RequestedByName).HasMaxLength(150);
        builder.Property(e => e.RequestedByEmail).HasMaxLength(150);
        builder.Property(e => e.RequestedByIp).HasMaxLength(45);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.StartedAt);
        builder.Property(e => e.FinishedAt);
        builder.Property(e => e.MessageCount).IsRequired();
        builder.Property(e => e.DocumentCount).IsRequired();
        builder.Property(e => e.ProcessedCount).IsRequired();
        builder.Property(e => e.RejectedCount).IsRequired();
        builder.Property(e => e.VoucherCount).IsRequired();
        builder.Property(e => e.TotalDebit).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.TotalCredit).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.ResultSummaryJson);
        builder.Ignore(e => e.EstaCerrado);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Number).IsUnique().HasDatabaseName("UK_COR_IntegrationBatches_Number");
        builder.HasIndex(e => new { e.ScheduleKey, e.ScheduledFor }).IsUnique()
            .HasDatabaseName(UnicoDeLaFranja).HasFilter("[Trigger] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.CashSessionPublicId).IsUnique()
            .HasDatabaseName(UnicoDeLaSesionDeCaja).HasFilter("[Trigger] = 2 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Destination, e.Status }).HasDatabaseName("IX_COR_IntegrationBatches_Destination_Status");
        builder.HasIndex(e => e.RequestedByUserId).HasDatabaseName("IX_COR_IntegrationBatches_RequestedByUser");

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

/// <summary><c>COR_IntegrationBatchCounters</c>: fila única con <c>NextValue</c> y <c>RowVersion</c>.</summary>
public class IntegrationBatchCounterConfiguration : IEntityTypeConfiguration<IntegrationBatchCounter>
{
    public void Configure(EntityTypeBuilder<IntegrationBatchCounter> builder)
    {
        builder.ToTable("COR_IntegrationBatchCounters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_IntegrationBatchCounters_PublicId");

        builder.Property(e => e.NextValue).IsRequired();

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
