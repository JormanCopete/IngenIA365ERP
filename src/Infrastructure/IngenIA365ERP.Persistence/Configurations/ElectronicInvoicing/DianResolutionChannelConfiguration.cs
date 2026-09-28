using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_DianResolutionChannels</c> (feature 012, I4, T698; data-model §18; FR-064, FR-065): asociación prefijo ↔ canal
/// o software desde una fecha; único <c>(ResolutionId, ValidFrom)</c> entre vivos. <c>TechnicalKey</c> sólo en
/// resoluciones de factura. Llega con la migración <c>DocumentosElectronicos</c>.
/// </summary>
public class DianResolutionChannelConfiguration : IEntityTypeConfiguration<DianResolutionChannel>
{
    public const string Tabla = "COR_DianResolutionChannels";

    public void Configure(EntityTypeBuilder<DianResolutionChannel> builder)
    {
        builder.ComoEntidadDeFacturacionElectronica(Tabla);

        builder.Property(e => e.ResolutionId).IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SoftwareId).HasMaxLength(36);
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.TechnicalKey).HasMaxLength(100);

        builder.HasIndex(e => new { e.ResolutionId, e.ValidFrom })
            .IsUnique()
            .HasDatabaseName("UK_COR_DianResolutionChannels_Resolution_ValidFrom")
            .HasFilter("[IsDeleted] = 0");
    }
}
