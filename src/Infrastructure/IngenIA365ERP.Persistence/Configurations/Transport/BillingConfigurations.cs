using IngenIA365ERP.Domain.Entities.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Transport;

// Feature 020 (R9, R10, R13; data-model §2): servicios, suscripciones, políticas, corridas, cargos y asignaciones. El índice único
// (VehicleId, ServiceId, Period) entre cargos no anulados (Status 3) es lo que impide facturar dos veces (FR-031), incluso entre dos
// sesiones a la vez: la segunda pierde la carrera y la corrida lo cuenta como «ya estaba».

public class TransportServiceConfiguration : IEntityTypeConfiguration<TransportService>
{
    public void Configure(EntityTypeBuilder<TransportService> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_Services");
        builder.Property(e => e.ProductCode).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreditLineCode).HasMaxLength(10);
        builder.Property(e => e.AssociateCreditMeansCode).HasMaxLength(10);
        builder.Property(e => e.CustomerCreditMeansCode).HasMaxLength(10);
        builder.HasOne(e => e.Connector).WithMany().HasForeignKey(e => e.ConnectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.ProductPublicId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_TRN_Services_Product");
    }
}

public class VehicleSubscriptionConfiguration : IEntityTypeConfiguration<VehicleSubscription>
{
    public void Configure(EntityTypeBuilder<VehicleSubscription> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_Subscriptions");
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Service).WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.VehicleId, e.ServiceId }).HasDatabaseName("IX_TRN_Subscriptions_Vehicle_Service");
    }
}

public class SubscriptionPriceConfiguration : IEntityTypeConfiguration<SubscriptionPrice>
{
    public void Configure(EntityTypeBuilder<SubscriptionPrice> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_SubscriptionPrices");
        builder.Property(e => e.Price).HasPrecision(18, 2);
        builder.HasOne(e => e.Subscription).WithMany(s => s.Prices).HasForeignKey(e => e.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BillingPolicyConfiguration : IEntityTypeConfiguration<BillingPolicy>
{
    public void Configure(EntityTypeBuilder<BillingPolicy> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_BillingPolicies");
        builder.Property(e => e.SalesDocumentTypeCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.WarehouseCode).HasMaxLength(10);
        builder.Property(e => e.SalesPointCode).HasMaxLength(10);
        builder.HasIndex(e => e.ValidFrom).HasDatabaseName("IX_TRN_BillingPolicies_ValidFrom");
    }
}

public class BillingRunConfiguration : IEntityTypeConfiguration<BillingRun>
{
    public void Configure(EntityTypeBuilder<BillingRun> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_BillingRuns");
        builder.Property(e => e.Period).HasMaxLength(6).IsFixedLength().IsRequired();
        builder.Property(e => e.StartedBy).HasMaxLength(150).IsRequired();
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(e => e.OperationKey).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UK_TRN_BillingRuns_OperationKey");
        builder.HasIndex(e => new { e.Period, e.Status }).HasDatabaseName("IX_TRN_BillingRuns_Period_Status");
    }
}

public class ServiceChargeConfiguration : IEntityTypeConfiguration<ServiceCharge>
{
    public void Configure(EntityTypeBuilder<ServiceCharge> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_ServiceCharges");
        builder.Property(e => e.Period).HasMaxLength(6).IsFixedLength().IsRequired();
        builder.Property(e => e.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.HasOne(e => e.Vehicle).WithMany().HasForeignKey(e => e.VehicleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Service).WithMany().HasForeignKey(e => e.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.BillingRun).WithMany().HasForeignKey(e => e.BillingRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OwnerPerson).WithMany().HasForeignKey(e => e.OwnerPersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.VehicleId, e.ServiceId, e.Period }).IsUnique().HasFilter("[IsDeleted] = 0 AND [Status] <> 3")
            .HasDatabaseName("UK_TRN_ServiceCharges_Vehicle_Service_Period");
        builder.HasIndex(e => e.LoanPublicId).HasDatabaseName("IX_TRN_ServiceCharges_LoanPublicId");
        builder.HasIndex(e => e.PaymentPublicId).HasDatabaseName("IX_TRN_ServiceCharges_PaymentPublicId");
        builder.HasIndex(e => e.InvoicePublicId).HasDatabaseName("IX_TRN_ServiceCharges_InvoicePublicId");
        builder.HasIndex(e => e.InvoiceNumber).HasDatabaseName("IX_TRN_ServiceCharges_InvoiceNumber");
        builder.HasIndex(e => e.ExternalId).IsUnique().HasDatabaseName("UK_TRN_ServiceCharges_ExternalId");
    }
}

public class ChargeAllocationConfiguration : IEntityTypeConfiguration<ChargeAllocation>
{
    public void Configure(EntityTypeBuilder<ChargeAllocation> builder)
    {
        builder.ComoEntidadDeTransporte("TRN_ChargeAllocations");
        builder.Property(e => e.ReceiptNumber).HasMaxLength(40);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.HasOne(e => e.Charge).WithMany(c => c.Allocations).HasForeignKey(e => e.ChargeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ChargeId, e.SourceMessagePublicId }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [SourceMessagePublicId] IS NOT NULL")
            .HasDatabaseName("UK_TRN_ChargeAllocations_Charge_Message");
        builder.HasIndex(e => e.ReceiptPublicId).HasDatabaseName("IX_TRN_ChargeAllocations_ReceiptPublicId");
    }
}
