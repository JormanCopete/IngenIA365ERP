using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class UserTenantAssignmentConfiguration : IEntityTypeConfiguration<UserTenantAssignment>
{
    public void Configure(EntityTypeBuilder<UserTenantAssignment> builder)
    {
        builder.ToTable("SEC_UserTenantAssignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.TenantId }).IsUnique();
        builder.HasIndex(e => new { e.UserId, e.IsPrimary })
            .IsUnique()
            .HasFilter("[IsPrimary] = 1 AND [IsDeleted] = 0");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
