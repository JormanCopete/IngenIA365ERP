using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class SecurityModuleConfiguration : IEntityTypeConfiguration<SecurityModule>
{
    public void Configure(EntityTypeBuilder<SecurityModule> builder)
    {
        builder.ToTable("SEC_Modules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.ProgramCode }).IsUnique();

        builder.Property(e => e.ProgramCode).HasMaxLength(30);
        builder.Property(e => e.Description).HasMaxLength(100);
        builder.Property(e => e.ProgramType).HasMaxLength(2);

        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
