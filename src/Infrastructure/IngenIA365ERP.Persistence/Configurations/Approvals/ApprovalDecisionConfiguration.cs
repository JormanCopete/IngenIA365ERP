using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Approvals;

/// <summary>
/// <c>COR_ApprovalDecisions</c> (feature 012, T33, T081; data-model §21). Hecho de sólo inserción
/// (<c>IHechoInmutable</c>). Una aprobación por nivel: único <c>(RequestId, Level)</c> filtrado
/// <c>[Decision] = 1</c> (los rechazos no cuentan: rechazar cierra la solicitud). <c>Decision</c> y <c>Method</c> se
/// guardan como int; FK <c>Restrict</c> a la solicitud (lado de <see cref="ApprovalRequestConfiguration"/>) y a
/// <c>SEC_Users</c>. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class ApprovalDecisionConfiguration : IEntityTypeConfiguration<ApprovalDecision>
{
    public void Configure(EntityTypeBuilder<ApprovalDecision> builder)
    {
        builder.ToTable("COR_ApprovalDecisions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ApprovalDecisions_PublicId");

        builder.Property(e => e.RequestId).IsRequired();
        builder.Property(e => e.Level).IsRequired();
        builder.Property(e => e.Decision).HasConversion<int>().IsRequired();
        builder.Property(e => e.DecidedByName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.DecidedAt).IsRequired();
        builder.Property(e => e.Method).HasConversion<int>().IsRequired();
        builder.Property(e => e.CredentialPublicId);
        builder.Property(e => e.PermissionCodeUsed).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ContentSha256).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RequestId, e.Level })
            .IsUnique()
            .HasDatabaseName("UK_COR_ApprovalDecisions_RequestId_Level_Approved")
            .HasFilter("[Decision] = 1");

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
