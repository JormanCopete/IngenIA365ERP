using IngenIA365ERP.Domain.Entities.Lending.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Loans;

/// <summary><c>LND_LoanControls</c> (feature 013; data-model §5.2): una fila por crédito (único sin filtro), cerrojo y marca de agua.</summary>
public class LoanControlConfiguration : IEntityTypeConfiguration<LoanControl>
{
    public const string Tabla = "LND_LoanControls";

    public void Configure(EntityTypeBuilder<LoanControl> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.LastAccruedDate).IsRequired();
        builder.Property(e => e.Sequence).IsRequired();
        builder.Property(e => e.LastRegisteredAt).IsRequired();
        builder.Property(e => e.LastPositionDate);

        builder.HasOne(e => e.Loan).WithMany().HasForeignKey(e => e.LoanId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.LoanId).IsUnique().HasDatabaseName("UK_LND_LoanControls_LoanId");
    }
}
