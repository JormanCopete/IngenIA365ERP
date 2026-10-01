using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Persistence.Configurations.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core.Payments;

/// <summary>
/// <c>COR_CardAcquirers</c> (feature 012, I3, T582; data-model §16): código único entre vivos; FK <c>Restrict</c> a la persona
/// que es el tercero de la cuenta por cobrar a la red.
/// </summary>
public class CardAcquirerConfiguration : IEntityTypeConfiguration<CardAcquirer>
{
    public void Configure(EntityTypeBuilder<CardAcquirer> builder)
    {
        builder.ComoEntidadDeInventario("COR_CardAcquirers");

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_COR_CardAcquirers_Code").HasFilter("[IsDeleted] = 0");
        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}
