using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Core;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("COR_Courses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique().HasDatabaseName("UK_COR_Courses_PublicId");

        builder.Property(e => e.LegacyCode).HasMaxLength(10);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(40);
        builder.Property(e => e.Duration).HasDefaultValue(0);
        builder.Property(e => e.EducationType).HasDefaultValue(0);
        builder.Property(e => e.Percentage).HasPrecision(7, 2).HasDefaultValue(0m);
        builder.Property(e => e.Amount).HasPrecision(17, 2).HasDefaultValue(0m);

        // Relationships
        builder.HasOne(e => e.TeachingEntity).WithMany(en => en.Courses).HasForeignKey(e => e.TeachingEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Committee).WithMany(c => c.Courses).HasForeignKey(e => e.CommitteeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ActivityProgram).WithMany(ap => ap.Courses).HasForeignKey(e => e.ActivityProgramId).OnDelete(DeleteBehavior.Restrict);

        // Audit
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.CreatedBy).HasMaxLength(100);
        builder.Property(e => e.UpdatedBy).HasMaxLength(100);
        builder.Property(e => e.DeletedBy).HasMaxLength(100);
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
