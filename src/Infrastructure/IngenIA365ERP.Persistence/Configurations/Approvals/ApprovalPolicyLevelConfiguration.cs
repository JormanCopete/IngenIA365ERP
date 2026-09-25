using IngenIA365ERP.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Approvals;

/// <summary>
/// <c>COR_ApprovalPolicyLevels</c> (feature 012, T33, T081; data-model §21). Único <c>(PolicyId, Order)</c>; la FK a
/// la política es <c>Restrict</c> (se declara del lado de <see cref="ApprovalPolicyConfiguration"/>). La tabla llega
/// con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class ApprovalPolicyLevelConfiguration : IEntityTypeConfiguration<ApprovalPolicyLevel>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicyLevel> builder)
    {
        builder.ToTable("COR_ApprovalPolicyLevels");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ApprovalPolicyLevels_PublicId");

        builder.Property(e => e.PolicyId).IsRequired();
        builder.Property(e => e.Order).IsRequired();
        builder.Property(e => e.Threshold).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.PermissionCode).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => new { e.PolicyId, e.Order })
            .IsUnique()
            .HasDatabaseName("UK_COR_ApprovalPolicyLevels_PolicyId_Order");

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
