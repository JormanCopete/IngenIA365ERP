using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

/// <summary>
/// <c>SEC_PermissionAmountLimits</c> (feature 012, T34, T081; data-model §21). Único
/// <c>(RoleId, PermissionCode, ValidFrom)</c> filtrado a filas vivas; FK <c>Restrict</c> a <c>SEC_Roles</c>.
/// <c>MaxAmount</c> nulo = sin límite. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class PermissionAmountLimitConfiguration : IEntityTypeConfiguration<PermissionAmountLimit>
{
    public void Configure(EntityTypeBuilder<PermissionAmountLimit> builder)
    {
        builder.ToTable("SEC_PermissionAmountLimits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_SEC_PermissionAmountLimits_PublicId");

        builder.Property(e => e.RoleId).IsRequired();
        builder.Property(e => e.PermissionCode).HasMaxLength(100).IsRequired();
        builder.Property(e => e.MaxAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RoleId, e.PermissionCode, e.ValidFrom })
            .IsUnique()
            .HasDatabaseName("UK_SEC_PermissionAmountLimits_Role_Permission_ValidFrom")
            .HasFilter("[IsDeleted] = 0");

        builder.Property(e => e.RowVersion).IsRowVersion();

        // Audit
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
