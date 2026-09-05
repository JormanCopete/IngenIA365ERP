using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Una fila por vigencia: <c>(Code, ValidFrom)</c> es la clave natural (FR-010, FR-011).</summary>
public class PayrollLegalParameterConfiguration : IEntityTypeConfiguration<PayrollLegalParameter>
{
    public void Configure(EntityTypeBuilder<PayrollLegalParameter> builder)
    {
        builder.ToTable("PAY_LegalParameters");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Code).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(200);
        builder.Property(e => e.Value).HasPrecision(18, 4);

        builder.HasIndex(e => new { e.Code, e.ValidFrom }).IsUnique().HasDatabaseName("UK_PAY_LegalParameters_Code_ValidFrom");

        builder.HasMany(e => e.Ranges)
            .WithOne(r => r.LegalParameter)
            .HasForeignKey(r => r.LegalParameterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PayrollLegalParameterRangeConfiguration : IEntityTypeConfiguration<PayrollLegalParameterRange>
{
    public void Configure(EntityTypeBuilder<PayrollLegalParameterRange> builder)
    {
        builder.ToTable("PAY_LegalParameterRanges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.FromValue).HasPrecision(18, 4);
        builder.Property(e => e.ToValue).HasPrecision(18, 4);
        builder.Property(e => e.Rate).HasPrecision(9, 4);
        builder.Property(e => e.FixedValue).HasPrecision(18, 4);

        builder.HasIndex(e => new { e.LegalParameterId, e.Order }).HasDatabaseName("IX_PAY_LegalParameterRanges_Parameter_Order");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
