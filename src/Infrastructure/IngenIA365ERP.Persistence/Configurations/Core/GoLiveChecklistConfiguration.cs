using IngenIA365ERP.Domain.Entities.Core.GoLive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

/// <summary>
/// <c>COR_GoLiveChecklists</c> (feature 015, E-06, T0033; data-model §16.2): la lista de go/no-go. Sus ítems cuelgan con FK <c>Restrict</c>;
/// el código de un ítem es único en su lista.
/// </summary>
public class GoLiveChecklistConfiguration : IEntityTypeConfiguration<GoLiveChecklist>
{
    public const string Tabla = "COR_GoLiveChecklists";

    public void Configure(EntityTypeBuilder<GoLiveChecklist> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Environment).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.DecidedByName).HasMaxLength(100);
        builder.Property(e => e.DecisionReason).HasMaxLength(1000);

        builder.HasMany(e => e.Items).WithOne(i => i.Checklist).HasForeignKey(i => i.ChecklistId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary><c>COR_GoLiveChecklistItems</c> (data-model §16.2).</summary>
public class GoLiveChecklistItemConfiguration : IEntityTypeConfiguration<GoLiveChecklistItem>
{
    public const string Tabla = "COR_GoLiveChecklistItems";

    public void Configure(EntityTypeBuilder<GoLiveChecklistItem> builder)
    {
        builder.ToTable(Tabla);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName($"UK_{Tabla}_PublicId");
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.Property(e => e.Module).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.Evidence).HasMaxLength(2000);
        builder.Property(e => e.CheckedByName).HasMaxLength(100);
        builder.Property(e => e.OverrideApprovedByName).HasMaxLength(100);
        builder.Property(e => e.OverrideReason).HasMaxLength(1000);

        builder.HasIndex(e => new { e.ChecklistId, e.Code }).IsUnique().HasDatabaseName($"UK_{Tabla}_ChecklistId_Code");

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
