using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>
/// Terminaciones de contrato (feature 010, data-model §2.5). Una sola terminación viva por ficha:
/// el filtro <c>[Status] IN (0, 1)</c> se escribe como <b>dos</b> índices únicos (<c>= 0</c> y
/// <c>= 1</c>) porque el traductor de filtros para PostgreSQL sólo entiende comparaciones simples.
/// </summary>
public class EmploymentTerminationConfiguration : IEntityTypeConfiguration<EmploymentTermination>
{
    public void Configure(EntityTypeBuilder<EmploymentTermination> builder)
    {
        builder.ToTable("PAY_EmploymentTerminations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.ReinstatedBy).HasMaxLength(100);
        builder.Property(e => e.ReinstateReason).HasMaxLength(300);

        // Mismas columnas, dos índices: el nombre va en HasIndex (con las mismas propiedades EF
        // devuelve el mismo índice y el segundo pisaría al primero).
        builder.HasIndex(e => e.EmployeeId, "UK_PAY_EmploymentTerminations_Employee_Registered").IsUnique()
            .HasFilter("[Status] = 0 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.EmployeeId, "UK_PAY_EmploymentTerminations_Employee_Settled").IsUnique()
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => e.TerminationDate).HasDatabaseName("IX_PAY_EmploymentTerminations_Date");

        builder.HasOne(e => e.Employee).WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TerminationReason).WithMany().HasForeignKey(e => e.TerminationReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Deductions).WithOne(d => d.Termination).HasForeignKey(d => d.TerminationId).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.EstaViva);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
