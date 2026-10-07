using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLines</c> (feature 013, T0210; data-model §1.2): código único entre vivas (<c>CodigoDeCatalogo</c>, 10), inmutable.
/// </summary>
public class CreditLineConfiguration : IEntityTypeConfiguration<CreditLine>
{
    public const string Tabla = "LND_CreditLines";

    public void Configure(EntityTypeBuilder<CreditLine> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(300);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ClosedReason).HasConversion<int?>();
        builder.Property(e => e.ClosedAt);
        builder.Property(e => e.CloseNotes).HasMaxLength(300);
        builder.Property(e => e.UsuryBlockedSince);
        builder.Property(e => e.UsuryBlockedByValuePublicId);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_CreditLines_Code").HasFilter("[IsDeleted] = 0");
    }
}
