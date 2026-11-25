using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationEdits</c> (pedido del dueño del 2026-11-25; migración <c>EdicionDeSolicitudRadicada</c>): cada edición de una solicitud
/// radicada sin devolverla, con su motivo y lo que cambió. Hecho de sólo inserción. (nuevo)
/// </summary>
public class CreditApplicationEditConfiguration : IEntityTypeConfiguration<CreditApplicationEdit>
{
    public const string Tabla = "LND_CreditApplicationEdits";

    public void Configure(EntityTypeBuilder<CreditApplicationEdit> builder)
    {
        builder.ComoEntidadDeCartera(Tabla);

        builder.Property(e => e.StatusAtEdit).HasConversion<int>().IsRequired();
        builder.Property(e => e.EditedAt).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(CreditApplicationEdit.LargoDelMotivo).IsRequired();
        builder.Property(e => e.Changes).IsRequired();

        builder.HasOne<CreditApplication>().WithMany().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EditedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributionLevel>().WithMany().HasForeignKey(e => e.LevelBeforeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributionLevel>().WithMany().HasForeignKey(e => e.LevelAfterId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ApplicationId, e.EditedAt })
            .HasDatabaseName("IX_LND_CreditApplicationEdits_App_EditedAt");
    }
}
