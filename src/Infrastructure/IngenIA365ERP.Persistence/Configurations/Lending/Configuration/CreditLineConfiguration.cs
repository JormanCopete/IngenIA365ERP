using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Configuration;

/// <summary>
/// <c>LND_CreditLines</c> (feature 013, T0210; data-model §1.2): código único entre vivas (<c>CodigoDeCatalogo</c>, 10), inmutable.
/// Feature 014 (T012; data-model §1.1): <c>Nature</c> con defecto colocación —todas las existentes lo son— y una sola línea de aportes activa
/// por cooperativa (el asociado tiene una sola cuenta de aportes).
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
        builder.Property(e => e.Nature).HasConversion<int>().IsRequired().HasDefaultValue(LineNature.Placement);
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.ClosedReason).HasConversion<int?>();
        builder.Property(e => e.ClosedAt);
        builder.Property(e => e.CloseNotes).HasMaxLength(300);
        builder.Property(e => e.UsuryBlockedSince);
        builder.Property(e => e.UsuryBlockedByValuePublicId);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_LND_CreditLines_Code").HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.Nature, e.Status }).HasDatabaseName("IX_LND_CreditLines_Nature_Status");
        // Feature 014 (T012): una sola línea de aportes activa (Nature = Contribution 3, Status = Active 2).
        builder.HasIndex(e => e.Nature).IsUnique().HasDatabaseName("UK_LND_CreditLines_ActiveContribution")
            .HasFilter("[Nature] = 3 AND [Status] = 2 AND [IsDeleted] = 0");
    }
}
