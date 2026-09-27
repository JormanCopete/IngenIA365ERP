using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Payments;

/// <summary>
/// <c>COR_CashDenominations</c> (feature 012, I3, T582; data-model §16): único <c>(Currency, Kind, Value)</c> entre vivos; valor en
/// pesos (18,2). Lo siembra <c>CashDenominationsSeeder</c> (Order 85).
/// </summary>
public class CashDenominationConfiguration : IEntityTypeConfiguration<CashDenomination>
{
    public void Configure(EntityTypeBuilder<CashDenomination> builder)
    {
        builder.ComoEntidadDeInventario("COR_CashDenominations");

        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(e => e.Value).Monto().IsRequired();
        builder.Property(e => e.Kind).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasIndex(e => new { e.Currency, e.Kind, e.Value }).IsUnique()
            .HasDatabaseName("UK_COR_CashDenominations_Currency_Kind_Value").HasFilter("[IsDeleted] = 0");
    }
}
