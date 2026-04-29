using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CollectionNoticeParamConfiguration : IEntityTypeConfiguration<CollectionNoticeParam>
{
    public void Configure(EntityTypeBuilder<CollectionNoticeParam> builder)
    {
        builder.ToTable("LND_CollectionNoticeParams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CollectionNoticeParams_PublicId");

        builder.Property(e => e.NoticeCode).HasMaxLength(3).IsRequired();
        builder.Property(e => e.CreatorName).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Position).HasMaxLength(60).IsRequired();
        builder.HasIndex(e => e.NoticeCode).IsUnique().HasDatabaseName("UK_LND_CollectionNoticeParams_Code");


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
