using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="TenantMfaPolicy"/> a <c>ADM_TenantMfaPolicies</c> alineado
/// con DDL 15c. UNIQUE TenantId — un registro por tenant.
/// </summary>
public class TenantMfaPolicyConfiguration : IEntityTypeConfiguration<TenantMfaPolicy>
{
    public void Configure(EntityTypeBuilder<TenantMfaPolicy> builder)
    {
        builder.ToTable("ADM_TenantMfaPolicies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.TenantId).IsRequired();
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_TenantMfaPolicies_TenantId");

        builder.Property(e => e.IsRequired).HasDefaultValue(false);

        // Entero explícito, no el nombre del enum: la máscara es aritmética de bits
        // y guardarla como texto obligaría a leer «Totp, WebAuthn» y volver a
        // parsearlo. El default en la BASE importa tanto como el de la entidad —
        // las filas que ya existen se rellenan con él al migrar, y si fuera 0
        // (Ninguno) toda cooperativa que hoy exige MFA quedaría en el estado
        // imposible que la entidad prohíbe.
        builder.Property(e => e.AllowedMethodsMask)
            .HasConversion<int>()
            .HasDefaultValue(ConversionDeMetodosMfa.Todos);

        builder.Property(e => e.CreatedAt);

        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
