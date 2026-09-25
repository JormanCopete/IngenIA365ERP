using IngenIA365ERP.Domain.Entities.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Parameters;

/// <summary>
/// <c>COR_ParameterVersions</c> (feature 012, T21, T069; data-model §4.1). <c>ScopeKind</c> se guarda como int y
/// <c>ScopeId</c> no tiene FK (es polimórfico: lo valida <c>AddParameterVersionCommand</c>). El único de
/// (Module, Key, ScopeKind, ScopeId, ValidFrom) va filtrado a filas vivas: una vigencia futura retirada con motivo
/// deja volver a registrar esa fecha. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class ParameterVersionConfiguration : IEntityTypeConfiguration<ParameterVersion>
{
    public void Configure(EntityTypeBuilder<ParameterVersion> builder)
    {
        builder.ToTable("COR_ParameterVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_ParameterVersions_PublicId");

        builder.Property(e => e.Module).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(80).IsRequired();
        builder.Property(e => e.ScopeKind).HasConversion<int>().IsRequired();
        builder.Property(e => e.ScopeId).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.LegalSource).HasMaxLength(200);

        builder.HasIndex(e => new { e.Module, e.Key, e.ScopeKind, e.ScopeId, e.ValidFrom })
            .IsUnique()
            .HasDatabaseName("UK_COR_ParameterVersions_Module_Key_Scope_ValidFrom")
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
