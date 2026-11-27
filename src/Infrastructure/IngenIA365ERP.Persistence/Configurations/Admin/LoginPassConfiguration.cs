using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

/// <summary>Feature 027: mapea <see cref="LoginPass"/> a <c>ADM_LoginPasses</c>.</summary>
public class LoginPassConfiguration : IEntityTypeConfiguration<LoginPass>
{
    public void Configure(EntityTypeBuilder<LoginPass> builder)
    {
        builder.ToTable("ADM_LoginPasses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.CentralUserId).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.GrantedByUserId).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(LoginPass.LargoMaximoDelMotivo).IsRequired();

        // La huella es la única forma de resolver el enlace: índice único (entre no borrados), 32 bytes exactos.
        builder.Property(e => e.TokenHash).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_ADM_LoginPasses_TokenHash");

        // Los pases de una persona en una cooperativa (ficha, reemplazo, tope semanal) y los de la cooperativa por fecha (tope diario).
        builder.HasIndex(e => new { e.CentralUserId, e.TenantId })
            .HasDatabaseName("IX_ADM_LoginPasses_CentralUserId_TenantId")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => new { e.TenantId, e.GrantedAt })
            .HasDatabaseName("IX_ADM_LoginPasses_TenantId_GrantedAt")
            .HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.SecurityStampAtGrant).HasMaxLength(256);
        builder.Property(e => e.LastUsedIp).HasMaxLength(45);
        builder.Property(e => e.VoidReason).HasMaxLength(64);

        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.UpdatedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        builder.Property(e => e.RowVersion).IsRowVersion();

        // No se borran: se anulan. Son el rastro de quién dejó entrar a quién sin segundo factor.
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
