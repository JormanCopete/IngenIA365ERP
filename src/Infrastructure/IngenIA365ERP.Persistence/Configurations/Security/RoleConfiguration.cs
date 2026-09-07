using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("SEC_Roles");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Code).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);

        // Único por tenant. Para SaaS-global (TenantId NULL), filtro adicional
        // garantiza que solo un rol con cada Code exista globalmente.
        builder.HasIndex(e => new { e.TenantId, e.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_SEC_Roles_Tenant_Code_NotDeleted");

        builder.Property(e => e.IsBuiltIn).HasDefaultValue(false);
        builder.Property(e => e.IsAssignable).HasDefaultValue(true);

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
