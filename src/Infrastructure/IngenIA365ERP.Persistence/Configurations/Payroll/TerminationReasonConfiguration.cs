using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Catálogo de motivos de retiro (feature 010, data-model §2.5): <c>Code</c> único entre vivas (<c>CodigoDeCatalogo</c>).</summary>
public class TerminationReasonConfiguration : IEntityTypeConfiguration<TerminationReason>
{
    public void Configure(EntityTypeBuilder<TerminationReason> builder)
    {
        builder.ToTable("PAY_TerminationReasons");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Code).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.LegalBasis).HasMaxLength(120);

        builder.HasIndex(e => e.Code).IsUnique()
            .HasDatabaseName("UK_PAY_TerminationReasons_Code")
            .HasFilter("[IsDeleted] = 0");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
