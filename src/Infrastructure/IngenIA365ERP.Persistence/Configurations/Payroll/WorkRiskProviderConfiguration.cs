using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

public class WorkRiskProviderConfiguration : IEntityTypeConfiguration<WorkRiskProvider>
{
    public void Configure(EntityTypeBuilder<WorkRiskProvider> builder)
    {
        builder.ToTable("PAY_WorkRiskProviders");
        builder.HasOne(e => e.Person).WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict); // feature 009 (FR-088)
        builder.HasIndex(e => e.PersonId);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();
        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();

        // Feature 010 (US5): código PILA de la administradora, único entre las vivas que lo tienen.
        builder.Property(e => e.PilaCode).HasMaxLength(6);
        builder.HasIndex(e => e.PilaCode).IsUnique().HasFilter("[PilaCode] IS NOT NULL AND [IsDeleted] = 0").HasDatabaseName("UK_PAY_WorkRiskProviders_PilaCode");

        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.TaxId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Factor).HasPrecision(8, 4);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
