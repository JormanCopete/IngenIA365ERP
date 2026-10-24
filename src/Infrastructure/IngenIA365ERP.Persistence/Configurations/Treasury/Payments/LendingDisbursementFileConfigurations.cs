using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury.Payments;

// Feature 015, US10 (T0267; E-01 c; data-model §10.8): el archivo de desembolsos que Cartera informa a Tesorería, con sus órdenes. Sin FK a LND_: el archivo y las
// órdenes de Cartera van por su PublicId; FK Restrict a TES_BankAccounts y TES_PaymentOrders.

/// <summary><c>TES_LendingDisbursementFiles</c>: único por archivo de Cartera y por el mensaje que lo trajo. (nuevo)</summary>
public class LendingDisbursementFileConfiguration : IEntityTypeConfiguration<LendingDisbursementFile>
{
    public const string Tabla = "TES_LendingDisbursementFiles";

    public void Configure(EntityTypeBuilder<LendingDisbursementFile> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne<OwnBankAccount>().WithMany().HasForeignKey(e => e.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.FileNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(e => e.FormatCode).HasMaxLength(30);
        builder.Property(e => e.SourceBankCode).HasMaxLength(20);
        builder.Property(e => e.SourceAccountNumber).HasMaxLength(40);
        builder.Property(e => e.Total).Monto();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.GeneratedByName).HasMaxLength(150).IsRequired();
        builder.HasIndex(e => e.FilePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_FilePublicId");
        builder.HasIndex(e => e.FileMessagePublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_FileMessagePublicId");
        builder.HasIndex(e => e.BankAccountId).HasDatabaseName($"IX_{Tabla}_BankAccountId");
    }
}

/// <summary><c>TES_LendingDisbursementFileOrders</c>: una fila por orden del archivo. (nuevo)</summary>
public class LendingDisbursementFileOrderConfiguration : IEntityTypeConfiguration<LendingDisbursementFileOrder>
{
    public const string Tabla = "TES_LendingDisbursementFileOrders";

    public void Configure(EntityTypeBuilder<LendingDisbursementFileOrder> builder)
    {
        builder.ComoEntidadDeTesoreria(Tabla);
        builder.HasOne(e => e.LendingDisbursementFile).WithMany(f => f.Orders).HasForeignKey(e => e.LendingDisbursementFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentOrder>().WithMany().HasForeignKey(e => e.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(e => e.OrderNumber).HasMaxLength(30).IsRequired();
        builder.Property(e => e.NetAmount).Monto();
        builder.HasIndex(e => new { e.LendingDisbursementFileId, e.OriginOrderPublicId }).IsUnique().HasDatabaseName($"UK_{Tabla}_File_Order");
        builder.HasIndex(e => e.OriginOrderPublicId).HasDatabaseName($"IX_{Tabla}_OriginOrderPublicId");
        builder.HasIndex(e => e.PaymentOrderId).HasDatabaseName($"IX_{Tabla}_PaymentOrderId");
    }
}
