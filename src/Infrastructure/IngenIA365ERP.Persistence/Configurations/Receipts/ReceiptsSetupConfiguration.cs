using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

/// <summary>
/// <c>RCD_Setup</c> (feature 015, T0036; data-model §1.1): fila única de Recaudo, nivel 1 del cerrojo. La siembra <c>RecaudoNucleo</c> con
/// <c>Id = 1</c> (<c>INSERT … WHERE NOT EXISTS</c>).
/// </summary>
public class ReceiptsSetupConfiguration : IEntityTypeConfiguration<ReceiptsSetup>
{
    public const string Tabla = "RCD_Setup";

    public void Configure(EntityTypeBuilder<ReceiptsSetup> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.ActivatedOn);
        builder.Property(e => e.CutoffTime);
        builder.Property(e => e.ContractVersionObligations).HasDefaultValue((short)1);
        builder.Property(e => e.ContractVersionAccounts).HasDefaultValue((short)1);
    }
}

/// <summary>
/// <c>RCD_PayerControls</c> (feature 015, T0036; data-model §3.1): una fila de cerrojo por persona (<c>UK_RCD_PayerControls_PersonId</c>, sin
/// filtro: es control, nunca se da de baja), con FK <c>Restrict</c> a <c>COR_People</c>. La crea <c>CerrojoDeRecaudo</c> si falta.
/// </summary>
public class PayerControlConfiguration : IEntityTypeConfiguration<PayerControl>
{
    public const string Tabla = "RCD_PayerControls";

    public void Configure(EntityTypeBuilder<PayerControl> builder)
    {
        builder.ComoEntidadDeRecaudo(Tabla);
        builder.HasOne<IngenIA365ERP.Domain.Entities.Core.Person>().WithMany().HasForeignKey(e => e.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.PersonId).IsUnique().HasDatabaseName($"UK_{Tabla}_PersonId");
    }
}
