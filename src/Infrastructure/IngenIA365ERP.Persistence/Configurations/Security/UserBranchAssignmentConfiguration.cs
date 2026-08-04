using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class UserBranchAssignmentConfiguration : IEntityTypeConfiguration<UserBranchAssignment>
{
    public void Configure(EntityTypeBuilder<UserBranchAssignment> builder)
    {
        builder.ToTable("SEC_UserBranchAssignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.TenantId, e.BranchId }).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.TenantId, e.IsDefault })
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
