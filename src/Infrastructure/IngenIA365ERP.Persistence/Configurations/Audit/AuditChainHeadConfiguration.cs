using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Audit;

/// <summary>
/// <c>COR_AuditChainHeads</c> (feature 012, T061; data-model §23, §24): una fila por flujo
/// (<c>Stream</c> único). El <c>RowVersion</c> (convención: <c>rowversion</c> / <c>xmin</c>) es lo que
/// impide bifurcar la cadena cuando dos réplicas sellan a la vez.
/// </summary>
public class AuditChainHeadConfiguration : IEntityTypeConfiguration<AuditChainHead>
{
    public void Configure(EntityTypeBuilder<AuditChainHead> builder)
    {
        builder.ToTable("COR_AuditChainHeads");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_AuditChainHeads_PublicId");

        builder.Property(e => e.Stream).HasMaxLength(60).IsRequired();
        builder.HasIndex(e => e.Stream).IsUnique().HasDatabaseName("UK_COR_AuditChainHeads_Stream");

        builder.Property(e => e.LastSeq).IsRequired();
        builder.Property(e => e.LastHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();

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
