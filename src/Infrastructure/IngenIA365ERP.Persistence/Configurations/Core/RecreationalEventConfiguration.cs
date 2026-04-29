using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class RecreationalEventConfiguration : IEntityTypeConfiguration<RecreationalEvent>
{
    public void Configure(EntityTypeBuilder<RecreationalEvent> builder)
    {
        builder.ToTable("COR_RecreationalEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_RecreationalEvents_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ActivityType).HasMaxLength(2);
        builder.Property(e => e.Percentage).HasPrecision(7, 2).HasDefaultValue(0m);
        builder.Property(e => e.Amount).HasPrecision(17, 2).HasDefaultValue(0m);
        builder.Property(e => e.ActivitySubtype).HasMaxLength(60);
        builder.Property(e => e.ControlNovelty).HasDefaultValue(false);

        // Relationships
        builder.HasOne(e => e.Committee).WithMany(c => c.RecreationalEvents).HasForeignKey(e => e.CommitteeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ActivityProgram).WithMany(ap => ap.RecreationalEvents).HasForeignKey(e => e.ActivityProgramId).OnDelete(DeleteBehavior.Restrict);

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
