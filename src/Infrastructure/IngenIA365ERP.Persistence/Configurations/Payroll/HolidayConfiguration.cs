using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Calendario de festivos (feature 010, data-model §2.2): único por fecha entre vivas, índice por año.</summary>
public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("PAY_Holidays");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(80).IsRequired();

        builder.HasIndex(e => e.Date).IsUnique()
            .HasDatabaseName("UK_PAY_Holidays_Date")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.Year).HasDatabaseName("IX_PAY_Holidays_Year");

        builder.Ignore(e => e.EsSembrado);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
