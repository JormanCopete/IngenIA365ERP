using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Admin;

public class TenantBranchConfiguration : IEntityTypeConfiguration<TenantBranch>
{
    public void Configure(EntityTypeBuilder<TenantBranch> builder)
    {
        builder.ToTable("ADM_Branches");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.IsHeadquarters })
            .IsUnique()
            .HasFilter("[IsHeadquarters] = 1 AND [IsDeleted] = 0");

        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(300);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(200);

        builder.HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
