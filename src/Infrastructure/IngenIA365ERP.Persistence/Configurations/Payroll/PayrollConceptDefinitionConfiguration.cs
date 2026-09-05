using IngenIA365ERP.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Payroll;

/// <summary>Una fila por versión: <c>(Code, ValidFrom)</c> es la clave natural (FR-029).</summary>
public class PayrollConceptDefinitionConfiguration : IEntityTypeConfiguration<PayrollConceptDefinition>
{
    public void Configure(EntityTypeBuilder<PayrollConceptDefinition> builder)
    {
        builder.ToTable("PAY_ConceptDefinitions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.Code).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.PercentParameterCode).HasMaxLength(40);
        builder.Property(e => e.AmountParameterCode).HasMaxLength(40);
        builder.Property(e => e.TableParameterCode).HasMaxLength(40);
        builder.Property(e => e.ComponentConceptCodes).HasMaxLength(400);

        builder.Property(e => e.FixedAmount).HasPrecision(18, 2);
        builder.Property(e => e.Percent).HasPrecision(9, 4);
        builder.Property(e => e.UnitFactor).HasPrecision(9, 4);
        builder.Property(e => e.MaxQuantity).HasPrecision(10, 2);
        builder.Property(e => e.MaxAmount).HasPrecision(18, 2);

        builder.HasIndex(e => new { e.Code, e.ValidFrom }).IsUnique().HasDatabaseName("UK_PAY_ConceptDefinitions_Code_ValidFrom");
        builder.HasIndex(e => new { e.Code, e.ValidTo }).HasDatabaseName("IX_PAY_ConceptDefinitions_Code_ValidTo");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

/// <summary>Cuentas por concepto (código) y centro de costo; FK a <c>ChartOfAccounts</c>, no códigos de texto.</summary>
public class PayrollConceptDefinitionAccountConfiguration : IEntityTypeConfiguration<PayrollConceptDefinitionAccount>
{
    public void Configure(EntityTypeBuilder<PayrollConceptDefinitionAccount> builder)
    {
        builder.ToTable("PAY_ConceptDefinitionAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();

        builder.Property(e => e.PublicId);
        builder.HasIndex(e => e.PublicId).IsUnique();

        builder.Property(e => e.ConceptCode).HasMaxLength(30).IsRequired();

        // Una fila por defecto (sin centro de costo) y una por centro de costo. Dos
        // indices filtrados porque un unico indice con la columna nullable se comporta
        // distinto en cada motor (SQL Server: un solo NULL; PostgreSQL: NULLs distintos).
        builder.HasIndex(e => e.ConceptCode)
            .IsUnique()
            .HasFilter("[CostCenterId] IS NULL")
            .HasDatabaseName("UX_PAY_ConceptDefinitionAccounts_Default");
        builder.HasIndex(e => new { e.ConceptCode, e.CostCenterId })
            .IsUnique()
            .HasFilter("[CostCenterId] IS NOT NULL")
            .HasDatabaseName("UX_PAY_ConceptDefinitionAccounts_CostCenter");

        builder.HasOne(e => e.CostCenter).WithMany().HasForeignKey(e => e.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.DebitAccount).WithMany().HasForeignKey(e => e.DebitAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CreditAccount).WithMany().HasForeignKey(e => e.CreditAccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
