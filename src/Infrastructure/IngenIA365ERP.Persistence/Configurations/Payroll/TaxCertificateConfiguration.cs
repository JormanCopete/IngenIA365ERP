using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class TaxCertificateConfiguration : IEntityTypeConfiguration<TaxCertificate>
{
    public void Configure(EntityTypeBuilder<TaxCertificate> builder)
    {
        builder.ToTable("PAY_TaxCertificates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.PeriodCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.Value34).HasPrecision(18, 2);
        builder.Property(e => e.Value35).HasPrecision(18, 2);
        builder.Property(e => e.Value36).HasPrecision(18, 2);
        builder.Property(e => e.Value37).HasPrecision(18, 2);
        builder.Property(e => e.Value38).HasPrecision(18, 2);
        builder.Property(e => e.Value39).HasPrecision(18, 2);
        builder.Property(e => e.Value40).HasPrecision(18, 2);
        builder.Property(e => e.Value41).HasPrecision(18, 2);
        builder.Property(e => e.Value42).HasPrecision(18, 2);
        builder.Property(e => e.Value43).HasPrecision(18, 2);
        builder.Property(e => e.Threshold).HasPrecision(18, 2);
        builder.Property(e => e.Rate).HasPrecision(8, 4);
        builder.Property(e => e.IssuedAt).HasMaxLength(60).IsRequired();
        builder.Property(e => e.PayerName).HasMaxLength(60).IsRequired();
        builder.Property(e => e.PayerTaxId).HasMaxLength(20).IsRequired();

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
