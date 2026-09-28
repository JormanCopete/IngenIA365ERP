using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.ElectronicInvoicing;

/// <summary>
/// <c>COR_ElectronicEmissionSettings</c> (feature 012, I4, T698; data-model §18): la configuración de emisión con vigencia
/// sin cruces; único <c>(ValidFrom)</c> entre vivos. <c>CredentialKey</c> es sólo el nombre de la clave del Secret
/// (ninguna credencial toca la base). Llega con la migración <c>DocumentosElectronicos</c>.
/// </summary>
public class ElectronicEmissionSettingConfiguration : IEntityTypeConfiguration<ElectronicEmissionSetting>
{
    public const string Tabla = "COR_ElectronicEmissionSettings";

    public void Configure(EntityTypeBuilder<ElectronicEmissionSetting> builder)
    {
        builder.ComoEntidadDeFacturacionElectronica(Tabla);

        builder.Property(e => e.Mode).HasConversion<int>().IsRequired();
        builder.Property(e => e.ChannelCode).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Environment).HasConversion<int>().IsRequired();
        builder.Property(e => e.SoftwareId).HasMaxLength(36);
        builder.Property(e => e.TestSetId).HasMaxLength(36);
        builder.Property(e => e.TestSetAcceptedAt);
        builder.Property(e => e.CredentialKey).HasMaxLength(120);
        builder.Property(e => e.CredentialVerifiedAt);
        builder.Property(e => e.EmailDeliveryBy).HasConversion<int>().IsRequired();
        builder.Property(e => e.IssuerTaxId).HasMaxLength(15).IsRequired();
        builder.Property(e => e.IssuerCheckDigit).HasMaxLength(1).IsRequired();
        builder.Property(e => e.IssuerBusinessName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.IssuerAddress).HasMaxLength(150).IsRequired();
        builder.Property(e => e.IssuerMunicipalityDaneCode).HasMaxLength(5).IsRequired();
        builder.Property(e => e.IssuerEmail).HasMaxLength(150).IsRequired();
        builder.Property(e => e.IsEnabled).IsRequired();
        builder.Property(e => e.ValidFrom).IsRequired();
        builder.Property(e => e.ValidTo);
        builder.Property(e => e.Reason).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => e.ValidFrom)
            .IsUnique()
            .HasDatabaseName("UK_COR_ElectronicEmissionSettings_ValidFrom")
            .HasFilter("[IsDeleted] = 0");
    }
}
