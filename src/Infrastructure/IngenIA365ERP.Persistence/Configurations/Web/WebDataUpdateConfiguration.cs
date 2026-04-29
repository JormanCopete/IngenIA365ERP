using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Web;

public class WebDataUpdateConfiguration : IEntityTypeConfiguration<WebDataUpdate>
{
    public void Configure(EntityTypeBuilder<WebDataUpdate> builder)
    {
        builder.ToTable("WEB_DataUpdates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.NewAddress).HasMaxLength(250);
        builder.Property(e => e.NewPhone).HasMaxLength(30);
        builder.Property(e => e.NewMobilePhone).HasMaxLength(30);
        builder.Property(e => e.NewEmail).HasMaxLength(200);
        builder.Property(e => e.NewCity).HasMaxLength(100);
        builder.Property(e => e.NewDepartment).HasMaxLength(100);
        builder.Property(e => e.NewEmployerName).HasMaxLength(100);
        builder.Property(e => e.NewEmployerAddress).HasMaxLength(250);
        builder.Property(e => e.NewEmployerPhone).HasMaxLength(30);
        builder.Property(e => e.NewPosition).HasMaxLength(100);
        builder.Property(e => e.NewSalary).HasPrecision(18, 2);
        builder.Property(e => e.NewMaritalStatus).HasMaxLength(5);
        builder.Property(e => e.NewEducationLevel).HasMaxLength(5);
        builder.Property(e => e.NewHousingType).HasMaxLength(5);
        builder.Property(e => e.ProcessedBy).HasMaxLength(50);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
