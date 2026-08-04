using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class UserMenuAccessConfiguration : IEntityTypeConfiguration<UserMenuAccess>
{
    public void Configure(EntityTypeBuilder<UserMenuAccess> builder)
    {
        builder.ToTable("SEC_UserMenuAccess");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ProgramType).HasMaxLength(2);
        builder.Property(e => e.ProgramCode).HasMaxLength(30);
        builder.Property(e => e.ProgramName).HasMaxLength(100);
        builder.Property(e => e.MenuCode).HasMaxLength(30);
        builder.Property(e => e.SubMenuCode).HasMaxLength(30);

        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
