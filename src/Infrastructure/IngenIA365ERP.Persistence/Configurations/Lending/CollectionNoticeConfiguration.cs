using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class CollectionNoticeConfiguration : IEntityTypeConfiguration<CollectionNotice>
{
    public void Configure(EntityTypeBuilder<CollectionNotice> builder)
    {
        builder.ToTable("LND_CollectionNotices");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_CollectionNotices_PublicId");

        builder.Property(e => e.NoticeNumber).HasMaxLength(3).IsRequired();
        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ConceptClass).HasMaxLength(2).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(60);
        builder.Property(e => e.SendToCodeudor).HasMaxLength(2).IsRequired();
        builder.Property(e => e.TotalAmount).HasPrecision(18, 2);


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
