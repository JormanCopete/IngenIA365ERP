using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Audit;

public class JournalChangeConfiguration : IEntityTypeConfiguration<JournalChange>
{
    public void Configure(EntityTypeBuilder<JournalChange> builder)
    {
        builder.ToTable("AUD_JournalChanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Action).HasMaxLength(10).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(100);
        builder.Property(e => e.IpAddress).HasMaxLength(50);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
