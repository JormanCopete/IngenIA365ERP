using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class PensionProviderConfiguration : IEntityTypeConfiguration<PensionProvider>
{
    public void Configure(EntityTypeBuilder<PensionProvider> builder)
    {
        builder.ToTable("PAY_PensionProviders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.HasIndex(e => e.Code).IsUnique();

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TaxId).HasMaxLength(20).IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
