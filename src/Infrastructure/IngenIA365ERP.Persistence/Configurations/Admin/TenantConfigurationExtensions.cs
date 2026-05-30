using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Configuración extra del <see cref="Tenant"/> (NIT, razón social, régimen
/// tributario, dirección legal). Vive en archivo separado para no tocar
/// <see cref="TenantConfiguration"/> existente.
/// </summary>
public class TenantLegalFieldsConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(e => e.Nit).HasMaxLength(20);
        builder.Property(e => e.LegalName).HasMaxLength(200);
        builder.Property(e => e.LegalAddress).HasMaxLength(300);
        builder.Property(e => e.TaxRegime).HasMaxLength(50);

        // NIT único (cuando está presente) — los nulos coexisten.
        builder.HasIndex(e => e.Nit)
            .IsUnique()
            .HasFilter("[Nit] IS NOT NULL AND [IsDeleted] = 0");
    }
}
