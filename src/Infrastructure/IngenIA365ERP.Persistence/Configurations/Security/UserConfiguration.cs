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

        // T025 (Feature 002) — las propiedades CentralUserId y
        // CentralUserPublicEmail viven en Domain.User para que US1/US2 puedan
        // referenciarlas sin tener que volver a tocar el modelo. Pero NO se
        // mapean a SEC_Users hoy porque el script 15d (T017) aún no se ha
        // ejecutado contra la BD — declarar el mapeo activo haría que EF
        // generara SELECTs con columnas inexistentes y rompería todos los
        // handlers legacy de Fase 0. Cuando se realice el cutover a identidad
        // central (US1/US2 + ejecutar 15d), se retirarán estos Ignore y se
        // activará el mapeo + UNIQUE index documentado en el script.
        builder.Ignore(e => e.CentralUserId);
        builder.Ignore(e => e.CentralUserPublicEmail);

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
