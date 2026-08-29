using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class UserBranchAssignmentConfiguration : IEntityTypeConfiguration<UserBranchAssignment>
{
    public void Configure(EntityTypeBuilder<UserBranchAssignment> builder)
    {
        builder.ToTable("SEC_UserBranchAssignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.TenantId, e.BranchId }).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.TenantId, e.IsDefault })
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.Ignore(e => e.Branch);   // ADM_Branches, misma frontera
    }
}
