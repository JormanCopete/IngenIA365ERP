using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Approvals;

/// <summary>
/// <c>COR_ApprovalRequests</c> (feature 012, T33, T081; data-model §21). Una sola pendiente por (fuente, sujeto):
/// único <c>(SourceType, SourcePublicId, Subject)</c> filtrado <c>[Status] = 0 AND [IsDeleted] = 0</c>; la bandeja
/// busca por <c>(Status, Module, CurrentLevel)</c>. <c>Status</c> se guarda como int. FK <c>Restrict</c> a la
/// política sellada y a <c>SEC_Users</c> (creador y solicitante). La tabla llega con la migración
/// <c>PlataformaParaInventario</c>.
/// </summary>
public class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("COR_ApprovalRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ApprovalRequests_PublicId");

        builder.Property(e => e.Module).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SourceType).HasMaxLength(60).IsRequired();
        builder.Property(e => e.SourcePublicId).IsRequired();
        builder.Property(e => e.SourceLabel).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ScopeWarehousePublicId);
        builder.Property(e => e.ScopePointOfSalePublicId);
        builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.OperationDate).IsRequired();
        builder.Property(e => e.RequiredLevelsJson).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.ExcludedUserIdsJson).HasMaxLength(400).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.CurrentLevel).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.RequestedAt).IsRequired();
        builder.Property(e => e.DecidedAt);

        builder.HasOne(e => e.Policy).WithMany().HasForeignKey(e => e.PolicyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Decisions).WithOne(d => d.Request).HasForeignKey(d => d.RequestId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SourceType, e.SourcePublicId, e.Subject })
            .IsUnique()
            .HasDatabaseName("UK_COR_ApprovalRequests_Source_Subject_Pending")
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.Status, e.Module, e.CurrentLevel })
            .HasDatabaseName("IX_COR_ApprovalRequests_Status_Module_CurrentLevel");

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
