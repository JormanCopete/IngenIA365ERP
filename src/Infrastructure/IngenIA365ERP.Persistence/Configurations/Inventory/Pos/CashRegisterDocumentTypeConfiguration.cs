using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory.Pos;

/// <summary>
/// <c>INV_CashRegisterDocumentTypes</c> (feature 012, I3, T583; FR-058, FR-067; data-model §15): un rol por caja entre vivos.
/// </summary>
public class CashRegisterDocumentTypeConfiguration : IEntityTypeConfiguration<CashRegisterDocumentType>
{
    public void Configure(EntityTypeBuilder<CashRegisterDocumentType> builder)
    {
        builder.ComoEntidadDeInventario("INV_CashRegisterDocumentTypes");

        builder.Property(e => e.Role).IsRequired();

        builder.HasOne(e => e.CashRegister).WithMany(c => c.DocumentTypes).HasForeignKey(e => e.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DocumentType).WithMany().HasForeignKey(e => e.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CashRegisterId, e.Role }).IsUnique()
            .HasDatabaseName("UK_INV_CashRegisterDocumentTypes_Register_Role").HasFilter("[IsDeleted] = 0");
    }
}
