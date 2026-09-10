using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>
/// Mapea <see cref="PlatformMfaPolicy"/> a <c>ADM_PlatformMfaPolicy</c>: una sola
/// fila para toda la plataforma.
///
/// <para>
/// La unicidad se defiende con un índice único sobre <c>Scope</c>, que sólo tiene
/// un valor legal. Es la misma forma que ya usan <c>TenantMfaPolicyConfiguration</c>
/// y <c>PasswordPolicyConfiguration</c> —índice único filtrado sobre la columna de
/// ámbito— y no una construcción nueva: aquí el ámbito es la plataforma entera, y
/// por eso la columna tiene un único valor posible en lugar de un tenant.
/// </para>
/// </summary>
public class PlatformMfaPolicyConfiguration : IEntityTypeConfiguration<PlatformMfaPolicy>
{
    public void Configure(EntityTypeBuilder<PlatformMfaPolicy> builder)
    {
        builder.ToTable("ADM_PlatformMfaPolicy");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Scope).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.Scope)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_PlatformMfaPolicy_Scope");

        // Ver la nota en TenantMfaPolicyConfiguration: sin sentinel, un cero se
        // guardaba como «todos». Aquí el Domain no admite Ninguno, pero el aviso de
        // EF salía igual y la regla es la misma.
        builder.Property(e => e.AllowedMethodsMask)
            .HasConversion<int>()
            .HasDefaultValue(ConversionDeMetodosMfa.Todos)
            .HasSentinel(ConversionDeMetodosMfa.SinAsignar);

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Soft-delete como el resto (Principio VII). Borrar lógicamente esta fila
        // equivale a volver al valor por defecto —todos los métodos—, porque el
        // lector trata la ausencia como «sin restricción». No es un estado que la
        // aplicación ofrezca, pero si alguien llega a él, cae del lado que no
        // encierra al maestro.
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
