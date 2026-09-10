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
        // HasSentinel: el cero (Ninguno) ES un valor legítimo cuando la cooperativa
        // no exige segundo factor, y sin sentinel EF lo confundía con «no asignado»,
        // lo omitía del INSERT y la base ponía «todos». Lo avisaba Serilog en cada
        // arranque de producción. El default de la COLUMNA sigue siendo «todos»:
        // es lo que deja arrancar a la imagen anterior contra este esquema.
        builder.Property(e => e.AllowedMethodsMask)
            .HasConversion<int>()
            .HasDefaultValue(ConversionDeMetodosMfa.Todos)
            .HasSentinel(ConversionDeMetodosMfa.SinAsignar);

        builder.Property(e => e.AllowEmailRecovery).HasDefaultValue(false);

        // El default de la BASE tiene que ser 24, no 0. Sin decirlo, EF pone 0 —el
        // default del int— y toda fila existente quedaría con «cero horas de
        // espera», que es exactamente el diseño sin demora que se descartó: sin
        // espera no hay aviso que llegue a tiempo ni cancelación posible, y el
        // segundo factor pasa a valer lo que valga el buzón.
        builder.Property(e => e.EmailRecoveryDelayHours)
            .HasDefaultValue(TenantMfaPolicy.DemoraPorDefectoEnHoras);

        builder.Property(e => e.CreatedAt);

        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
