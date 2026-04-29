using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Web;

public class WebAffiliationApplicationConfiguration : IEntityTypeConfiguration<WebAffiliationApplication>
{
    public void Configure(EntityTypeBuilder<WebAffiliationApplication> builder)
    {
        builder.ToTable("WEB_AffiliationApplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.IdentificationNumber).HasMaxLength(20);
        builder.Property(e => e.IdentificationType).HasMaxLength(5);
        builder.Property(e => e.FirstName).HasMaxLength(150);
        builder.Property(e => e.LastName).HasMaxLength(150);
        builder.Property(e => e.Address).HasMaxLength(250);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.Property(e => e.EmployerName).HasMaxLength(100);
        builder.Property(e => e.Salary).HasPrecision(18, 2);
        builder.Property(e => e.SpouseFirstName).HasMaxLength(150);
        builder.Property(e => e.SpouseLastName).HasMaxLength(150);
        builder.Property(e => e.SpouseIdentificationNumber).HasMaxLength(20);
        builder.Property(e => e.SpouseIdentificationType).HasMaxLength(5);
        builder.Property(e => e.SpousePhone).HasMaxLength(30);
        builder.Property(e => e.SpouseEmail).HasMaxLength(200);
        builder.Property(e => e.SpouseEmployerName).HasMaxLength(100);
        builder.Property(e => e.SpouseSalary).HasPrecision(18, 2);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
