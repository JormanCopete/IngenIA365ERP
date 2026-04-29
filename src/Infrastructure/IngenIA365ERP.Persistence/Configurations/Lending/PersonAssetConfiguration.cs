using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class PersonAssetConfiguration : IEntityTypeConfiguration<PersonAsset>
{
    public void Configure(EntityTypeBuilder<PersonAsset> builder)
    {
        builder.ToTable("LND_PersonAssets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_PersonAssets_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AssetType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.AssetClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(80);
        builder.Property(e => e.Brand).HasMaxLength(80);
        builder.Property(e => e.Model).HasMaxLength(20);
        builder.Property(e => e.AssetValue).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
