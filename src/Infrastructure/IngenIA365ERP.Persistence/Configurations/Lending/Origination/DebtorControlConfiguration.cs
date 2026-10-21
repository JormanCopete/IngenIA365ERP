using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary><c>LND_DebtorControls</c> (feature 013; data-model §2.13): la fila del cerrojo por deudor; una por persona, sin filtro.</summary>
public class DebtorControlConfiguration : IEntityTypeConfiguration<DebtorControl>
{
    public const string Tabla = "LND_DebtorControls";

    public void Configure(EntityTypeBuilder<DebtorControl> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.HasOne<Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName("UK_LND_DebtorControls_PersonId");
    }
}
