using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("SEC_Users");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Username).IsUnique();

        builder.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(e => e.PasswordSalt).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.MfaSecret).HasMaxLength(200);
        builder.Property(e => e.LegacyLogin).HasMaxLength(50);
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20);
        builder.Property(e => e.CanApproveLoansMin).HasPrecision(18, 2);
        builder.Property(e => e.CanApproveLoansMax).HasPrecision(18, 2);

        builder.Property(e => e.IsSaasOperator).HasDefaultValue(false);
        builder.Property(e => e.MustChangePassword).HasDefaultValue(false);

        // El puente con la identidad central, por fin persistido.
        //
        // Estas dos columnas existían en el modelo de dominio y estaban
        // Ignore()-adas esperando un script que nunca se ejecutó. Mientras tanto,
        // el puente entre «quién inició sesión» y «qué fila de SEC_Users es» era
        // una comparación de cadenas por correo, repetida en dos sitios. Frágil de
        // dos formas: SEC_Users.Username es UNIQUE, y un cambio de correo en la
        // identidad central huérfana el mapeo en silencio.
        //
        // Ahora el esquema lo generan las migraciones de EF, así que retirar el
        // Ignore ES el cutover: no hace falta ningún script aparte.
        //
        // Índice NO único a propósito. La unicidad correcta sería «una fila por
        // identidad central», pero como la columna admite nulos —filas de sistema
        // sin identidad— un índice único dejaría pasar un solo nulo en SQL Server.
        // El invariante lo sostiene TenantUserProvisioner, que comprueba antes de
        // insertar; esto es para buscar rápido.
        builder.Property(e => e.CentralUserPublicEmail).HasMaxLength(200);
        builder.HasIndex(e => e.CentralUserId);

        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId);

        // El N:N User↔Role se materializa con la entidad explícita UserRole
        // (mapeada a SEC_UserRoles, FKs UserId/RoleId).
        //
        // Clave: usamos compuesta (UserId, RoleId) — más resiliente que `Id`
        // porque algunos entornos crearon SEC_UserRoles como junction
        // implícita de EF sin columna Id. Ignoramos las columnas heredadas
        // de AuditableEntity que no son responsabilidad de la junction
        // (PublicId, RowVersion, audit cols, IsDeleted) para que EF no
        // exija columnas que la tabla "junction-style" no necesariamente
        // tiene. AssignedAt/AssignedBy se mantienen como atributos
        // semánticos de la asignación.
        builder.HasMany(e => e.Roles).WithMany(r => r.Users)
            .UsingEntity<UserRole>(
                j => j.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne(ur => ur.User).WithMany().HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Restrict),
                j =>
                {
                    j.ToTable("SEC_UserRoles");
                    j.HasKey(ur => new { ur.UserId, ur.RoleId });
                    j.Ignore(ur => ur.Id);
                    j.Ignore(ur => ur.PublicId);
                    j.Ignore(ur => ur.RowVersion);
                    j.Ignore(ur => ur.IsDeleted);
                    j.Ignore(ur => ur.DeletedAt);
                    j.Ignore(ur => ur.DeletedBy);
                    j.Ignore(ur => ur.CreatedAt);
                    j.Ignore(ur => ur.CreatedBy);
                    j.Ignore(ur => ur.UpdatedAt);
                    j.Ignore(ur => ur.UpdatedBy);
                    j.Property(ur => ur.AssignedBy).HasMaxLength(100);
                    // Neutraliza el filtro automático de IsDeleted que añade
                    // ApplyBaseEntityConventions — la junction no maneja
                    // soft-delete (FR-029 aplica a entidades reales, no a la
                    // relación). Sin esto, la convención generaría un filter
                    // sobre IsDeleted que ya está Ignored → fallaría en runtime.
                    j.HasQueryFilter(ur => true);
                });

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
