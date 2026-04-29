using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Security;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("SEC_Users");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Username).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Username).IsUnique();

        builder.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(e => e.PasswordSalt).HasMaxLength(200);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.MfaSecret).HasMaxLength(200);
        builder.Property(e => e.LegacyLogin).HasMaxLength(50);
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20);
        builder.Property(e => e.CanApproveLoansMin).HasPrecision(18, 2);
        builder.Property(e => e.CanApproveLoansMax).HasPrecision(18, 2);

        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId);
        builder.HasMany(e => e.Roles).WithMany(r => r.Users)
            .UsingEntity("SEC_UserRoles");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
