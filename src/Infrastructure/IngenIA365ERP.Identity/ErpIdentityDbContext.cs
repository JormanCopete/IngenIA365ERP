using IngenIA365ERP.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Identity;

public class ErpIdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public DbSet<IdentityPermission> Permissions { get; set; }
    public DbSet<IdentityRolePermission> RolePermissions { get; set; }

    public ErpIdentityDbContext(DbContextOptions<ErpIdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Map Identity tables to SEC_ prefix
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("SEC_Users");
            b.HasIndex(u => u.PublicId).IsUnique();
            b.HasIndex(u => u.TenantId);
            b.Property(u => u.FullName).HasMaxLength(200);
            b.Property(u => u.TenantId).HasMaxLength(50);
            b.Property(u => u.RefreshToken).HasMaxLength(500);
            b.Property(u => u.LastLoginIp).HasMaxLength(50);
            b.Property(u => u.LastLoginUserAgent).HasMaxLength(500);
            b.Property(u => u.CreatedBy).HasMaxLength(100);
            b.Property(u => u.UpdatedBy).HasMaxLength(100);
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("SEC_Roles");
            b.HasIndex(r => r.PublicId).IsUnique();
            b.Property(r => r.TenantId).HasMaxLength(50);
            b.Property(r => r.Description).HasMaxLength(500);
        });

        builder.Entity<IdentityUserRole<int>>().ToTable("SEC_UserRoles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("SEC_UserClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("SEC_UserLogins");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("SEC_RoleClaims");
        builder.Entity<IdentityUserToken<int>>().ToTable("SEC_UserTokens");

        builder.Entity<IdentityPermission>(b =>
        {
            b.ToTable("SEC_Permissions");
            b.HasKey(p => p.Id);
            b.HasIndex(p => p.PublicId).IsUnique();
            b.HasIndex(p => p.PermissionCode).IsUnique();
            b.Property(p => p.Module).HasMaxLength(100);
            b.Property(p => p.Feature).HasMaxLength(100);
            b.Property(p => p.Action).HasMaxLength(50);
            b.Property(p => p.PermissionCode).HasMaxLength(200);
            b.Property(p => p.Description).HasMaxLength(500);
        });

        builder.Entity<IdentityRolePermission>(b =>
        {
            b.ToTable("SEC_RolePermissions");
            b.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            b.HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
