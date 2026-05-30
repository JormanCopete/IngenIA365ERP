using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class MfaResetRequestConfiguration : IEntityTypeConfiguration<MfaResetRequest>
{
    public void Configure(EntityTypeBuilder<MfaResetRequest> builder)
    {
        builder.ToTable("SEC_MfaResetRequests");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => new { e.UserId, e.Status })
            .HasDatabaseName("IX_SEC_MfaResetRequests_User_Status");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
