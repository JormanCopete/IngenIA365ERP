using IngenIA365ERP.Domain.Entities.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

public class BlacklistEntryConfiguration : IEntityTypeConfiguration<BlacklistEntry>
{
    public void Configure(EntityTypeBuilder<BlacklistEntry> builder)
    {
        builder.ToTable("LND_Blacklist");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_LND_Blacklist_PublicId");

        builder.Property(e => e.PersonCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IdType).HasMaxLength(2).IsRequired();
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CompanyName).HasMaxLength(80).IsRequired();
        builder.Property(e => e.PersonName).HasMaxLength(80);
        builder.Property(e => e.Address).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.IsImported).HasMaxLength(2).IsRequired();


        builder.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
