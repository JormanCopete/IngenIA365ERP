using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_TransferAccounts</c> (feature 022, T040; FR-046): punto, caja opcional, medio y cuenta propia de Tesorería (sólo su
/// <c>PublicId</c>, sin FK hacia <c>TES_</c>). Única entre vivas por punto, caja, medio y cuenta.
/// </summary>
public class SalesTransferAccountConfiguration : IEntityTypeConfiguration<SalesTransferAccount>
{
    public void Configure(EntityTypeBuilder<SalesTransferAccount> builder)
    {
        builder.ComoEntidadDeInventario("INV_TransferAccounts");

        builder.Property(e => e.AccountLabel).HasMaxLength(150).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasOne<PointOfSale>().WithMany().HasForeignKey(e => e.PointOfSaleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashRegister>().WithMany().HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PaymentMeans>().WithMany().HasForeignKey(e => e.PaymentMeansId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PointOfSaleId, e.CashRegisterId, e.PaymentMeansId, e.OwnBankAccountPublicId }).IsUnique()
            .HasDatabaseName("UK_INV_TransferAccounts_Point_Register_Means_Account").HasFilter("[IsDeleted] = 0");
    }
}
