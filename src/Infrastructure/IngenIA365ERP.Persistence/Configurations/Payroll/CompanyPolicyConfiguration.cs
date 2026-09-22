using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Políticas por empresa con vigencia (feature 010, data-model §2.1). Único <c>(Key, ValidFrom)</c>
/// filtrado a filas vivas; que dos vigencias de la misma clave no se crucen es regla del comando.
/// </summary>
public class CompanyPolicyConfiguration : IEntityTypeConfiguration<CompanyPolicy>
{
    public void Configure(EntityTypeBuilder<CompanyPolicy> builder)
    {
        builder.ToTable("PAY_CompanyPolicies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Key).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(400).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(300);

        builder.HasIndex(e => new { e.Key, e.ValidFrom }).IsUnique()
            .HasDatabaseName("UK_PAY_CompanyPolicies_Key_ValidFrom")
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
