using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class PasswordPolicyConfiguration : IEntityTypeConfiguration<PasswordPolicy>
{
    public void Configure(EntityTypeBuilder<PasswordPolicy> builder)
    {
        builder.ToTable("SEC_PasswordPolicies");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.MinLength).HasDefaultValue(12);
        builder.Property(e => e.ExpiryDays).HasDefaultValue(90);
        builder.Property(e => e.HistorySize).HasDefaultValue(5);
        builder.Property(e => e.LockoutThreshold).HasDefaultValue(5);
        builder.Property(e => e.LockoutMinutes).HasDefaultValue(15);

        // Una política activa por tenant (filtered index: ignora soft-deleted).
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_SEC_PasswordPolicies_TenantId_NotDeleted");

        // Sin clave foranea hacia ADM_Tenants, y no es una omision.
        //
        // El registro de cooperativas vive en la base ADMINISTRATIVA, no aqui. La
        // relacion sigue existiendo conceptualmente —TenantId dice de que
        // cooperativa es— pero se valida en la aplicacion, porque cuando cada
        // cooperativa tiene su propia base una clave foranea entre bases distintas
        // es sencillamente imposible de crear.
        //
        // Declararla no era inofensivo: EF arrastraba ADM_Tenants al modelo
        // operativo y la copiaba, vacia, dentro del espacio de cada cooperativa. La
        // FK resolvia contra esa copia local, asi que insertar una fila con TenantId
        // violaba la restriccion SIEMPRE. Es la razon por la que ninguna cooperativa
        // llego a tener roles y el sistema de permisos estuvo inerte.
        builder.Ignore(e => e.Tenant);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
