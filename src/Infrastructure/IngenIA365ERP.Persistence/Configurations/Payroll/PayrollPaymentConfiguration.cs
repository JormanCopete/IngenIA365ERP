using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Marca de pagado por empleado y corrida; un solo pago vigente por empleado (FR-040).</summary>
public class PayrollPaymentConfiguration : IEntityTypeConfiguration<PayrollPayment>
{
    public void Configure(EntityTypeBuilder<PayrollPayment> builder)
    {
        builder.ToTable("PAY_PayrollPayments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Reference).HasMaxLength(60);
        builder.Property(e => e.PaidBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RevertedBy).HasMaxLength(100);
        builder.Property(e => e.RevertReason).HasMaxLength(300);

        // Un pago vigente por empleado y corrida; los revertidos quedan como historial.
        builder.HasIndex(e => e.PayrollRunEmployeeId)
            .IsUnique()
            .HasFilter("[IsReverted] = 0")
            .HasDatabaseName("UX_PAY_PayrollPayments_RunEmployee_Vigente");

        builder.HasOne(e => e.RunEmployee).WithMany().HasForeignKey(e => e.PayrollRunEmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Cada intento de envío manual del comprobante de pago (FR-026).</summary>
public class PayslipDeliveryConfiguration : IEntityTypeConfiguration<PayslipDelivery>
{
    public void Configure(EntityTypeBuilder<PayslipDelivery> builder)
    {
        builder.ToTable("PAY_PayslipDeliveries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.RequestedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(500);

        builder.HasIndex(e => e.PayrollRunEmployeeId).HasDatabaseName("IX_PAY_PayslipDeliveries_RunEmployee");

        builder.HasOne(e => e.RunEmployee).WithMany().HasForeignKey(e => e.PayrollRunEmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
