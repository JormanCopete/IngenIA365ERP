using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Payments;

/// <summary>
/// <c>COR_CardTerminals</c> (feature 012, I3, T582; data-model §16): «Datáfonos de cobro», únicos por <c>(CardAcquirerId, Code)</c>
/// entre vivos.
/// </summary>
public class CardTerminalConfiguration : IEntityTypeConfiguration<CardTerminal>
{
    public void Configure(EntityTypeBuilder<CardTerminal> builder)
    {
        builder.ComoEntidadDeInventario("COR_CardTerminals");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Serial).HasMaxLength(40);
        builder.Property(e => e.Description).HasMaxLength(80);
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasOne(e => e.CardAcquirer).WithMany(a => a.Terminals).HasForeignKey(e => e.CardAcquirerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CardAcquirerId, e.Code }).IsUnique()
            .HasDatabaseName("UK_COR_CardTerminals_Acquirer_Code").HasFilter("[IsDeleted] = 0");
    }
}
