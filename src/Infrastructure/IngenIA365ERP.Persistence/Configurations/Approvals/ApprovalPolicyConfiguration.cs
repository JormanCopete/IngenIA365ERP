using IngenIA365ERP.Domain.Entities.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Approvals;

/// <summary>
/// <c>COR_ApprovalPolicies</c> (feature 012, T33, T081; data-model §21). <c>DocumentTypePublicId</c> sin FK: la
/// plataforma no apunta al módulo. Único <c>(PolicyKey, ValidFrom)</c> filtrado a filas vivas. La tabla llega con la
/// migración <c>PlataformaParaInventario</c>.
/// </summary>
public class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        builder.ToTable("COR_ApprovalPolicies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ApprovalPolicies_PublicId");

        builder.Property(e => e.Module).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(40).IsRequired();
        builder.Property(e => e.DocumentTypePublicId);
        builder.Property(e => e.PolicyKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasMany(e => e.Levels).WithOne(l => l.Policy).HasForeignKey(l => l.PolicyId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.PolicyKey, e.ValidFrom })
            .IsUnique()
            .HasDatabaseName("UK_COR_ApprovalPolicies_PolicyKey_ValidFrom")
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
