using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// <c>INV_DocumentPartySnapshots</c> (feature 012, T52, T136; data-model §5.6). Hecho de sólo inserción; único
/// <c>(DocumentId, Version)</c>. FK <c>Restrict</c> al documento y a la persona.
/// </summary>
public class DocumentPartySnapshotConfiguration : IEntityTypeConfiguration<DocumentPartySnapshot>
{
    public void Configure(EntityTypeBuilder<DocumentPartySnapshot> builder)
    {
        builder.ToTable("INV_DocumentPartySnapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_INV_DocumentPartySnapshots_PublicId");

        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.DianOrganizationType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.DianIdTypeCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.TaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CheckDigit).HasMaxLength(1).IsFixedLength();
        builder.Property(e => e.LegalName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(150);
        builder.Property(e => e.LastName).HasMaxLength(150);
        builder.Property(e => e.Address).HasMaxLength(200);
        builder.Property(e => e.MunicipalityDaneCode).HasMaxLength(5);
        builder.Property(e => e.CityName).HasMaxLength(100);
        builder.Property(e => e.DepartmentName).HasMaxLength(100);
        builder.Property(e => e.CountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.DianResponsibilities).HasMaxLength(100);
        builder.Property(e => e.DianTaxSchemeCode).HasMaxLength(10);
        builder.Property(e => e.CiiuCode).HasMaxLength(10);
        builder.Property(e => e.ChangeReason).HasMaxLength(300);

        builder.HasOne<InventoryDocument>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.DocumentId, e.Version }).IsUnique().HasDatabaseName("UK_INV_DocumentPartySnapshots_Document_Version");

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
