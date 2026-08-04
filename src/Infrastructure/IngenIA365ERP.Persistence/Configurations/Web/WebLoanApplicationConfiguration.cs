using IngenIA365ERP.Domain.Entities.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Web;

public class WebLoanApplicationConfiguration : IEntityTypeConfiguration<WebLoanApplication>
{
    public void Configure(EntityTypeBuilder<WebLoanApplication> builder)
    {
        builder.ToTable("WEB_LoanApplications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.InterestRate).HasPrecision(10, 5);
        builder.Property(e => e.RequestedAmount).HasPrecision(18, 2);
        builder.Property(e => e.InstallmentAmount).HasPrecision(18, 2);
        builder.Property(e => e.Periodicity).HasMaxLength(2);
        builder.Property(e => e.Purpose).HasMaxLength(300);
        builder.Property(e => e.LegacyCodigoTer).HasMaxLength(20);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
