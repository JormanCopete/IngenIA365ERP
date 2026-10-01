using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_BackgroundLeases</c> (feature 012, T047; data-model §0.1). El índice único sobre <c>Name</c> es
/// lo que hace que el <c>UPDATE … WHERE Name = @n AND (…)</c> de <c>ArrendamientosEnBase</c> toque a lo
/// sumo una fila; el <c>RowVersion</c> (convención: <c>rowversion</c> en SQL Server, <c>xmin</c> en
/// PostgreSQL) es la segunda defensa. Las cinco filas las siembra la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class BackgroundLeaseConfiguration : IEntityTypeConfiguration<BackgroundLease>
{
    public void Configure(EntityTypeBuilder<BackgroundLease> builder)
    {
        builder.ToTable("COR_BackgroundLeases");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_BackgroundLeases_PublicId");

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique().HasDatabaseName("UK_COR_BackgroundLeases_Name");

        builder.Property(e => e.Owner).HasMaxLength(150);
        builder.Property(e => e.LeaseUntil).IsRequired();

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
