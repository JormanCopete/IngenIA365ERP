using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class PasswordHistoryConfiguration : IEntityTypeConfiguration<PasswordHistory>
{
    public void Configure(EntityTypeBuilder<PasswordHistory> builder)
    {
        builder.ToTable("SEC_PasswordHistory");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        builder.HasIndex(e => new { e.UserId, e.SetAt })
            .HasDatabaseName("IX_SEC_PasswordHistory_User_SetAt");

        builder.HasOne(e => e.User)
            .WithMany(u => u.PasswordHistory)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
