using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing;

/// <summary>
/// La configuración de emisión de la cooperativa, con vigencia sin cruces (<c>COR_ElectronicEmissionSettings</c>;
/// feature 012, I4, T692; data-model §18; contracts/dian.md §10.1). <c>ConfigureEmissionCommand</c> crea la fila nueva
/// y cierra la anterior la víspera, con motivo. Cada documento electrónico <b>sella</b> la vigente al numerarse
/// (<c>EmissionSettingId</c>, <c>Mode</c>, <c>ChannelCode</c>, <c>SoftwareId</c>): cambiar de canal no toca lo ya
/// numerado (FR-064).
///
/// <para>
/// Ningún token, usuario, contraseña, certificado ni PIN se guarda aquí (<c>LasCredencialesDeFacturacionNoTocanLaBase</c>):
/// <see cref="CredentialKey"/> es sólo el <b>nombre</b> de la clave del Secret <c>erp-fe-credenciales</c>
/// (<c>{tenantPublicId}.{channelCode}.json</c>), y aun así va con <see cref="NoAuditarAttribute"/>.
/// </para>
/// </summary>
public class ElectronicEmissionSetting : AuditableEntity
{
    public EmissionMode Mode { get; set; }

    /// <summary>Clave del adaptador en mayúsculas (<c>SIMULADO</c>, la del proveedor, <c>SERVICIO-CENTRAL</c>; máx. 40).</summary>
    public string ChannelCode { get; set; } = string.Empty;

    public DianEnvironment Environment { get; set; } = DianEnvironment.Testing;

    /// <summary>Modo software propio (máx. 36).</summary>
    public string? SoftwareId { get; set; }

    /// <summary>Set de pruebas (máx. 36).</summary>
    public string? TestSetId { get; set; }

    /// <summary>En producción y con software propio, sin él la guardia bloquea.</summary>
    public DateTime? TestSetAcceptedAt { get; set; }

    /// <summary>
    /// Sólo el nombre de la clave del Secret (máx. 120). Si no coincide con la ruta derivada de la cooperativa resuelta:
    /// <c>ElectronicInvoicing.CredentialMismatch</c>.
    /// </summary>
    [NoAuditar]
    public string? CredentialKey { get; set; }

    /// <summary>Lo escribe <c>VerifyChannelCredentialCommand</c>.</summary>
    public DateTime? CredentialVerifiedAt { get; set; }

    /// <summary><c>Channel</c> sólo si <c>CapacidadesDelCanal</c> lo ofrece.</summary>
    public EmailDeliveryBy EmailDeliveryBy { get; set; } = EmailDeliveryBy.Erp;

    /// <summary>NIT del emisor (máx. 15), propuesto desde <c>COR_Companies</c> y confirmado aquí.</summary>
    public string IssuerTaxId { get; set; } = string.Empty;

    /// <summary>Dígito de verificación (1).</summary>
    public string IssuerCheckDigit { get; set; } = string.Empty;

    /// <summary>Razón social (máx. 200).</summary>
    public string IssuerBusinessName { get; set; } = string.Empty;

    /// <summary>Dirección (máx. 150).</summary>
    public string IssuerAddress { get; set; } = string.Empty;

    /// <summary>Municipio DIVIPOLA (5).</summary>
    public string IssuerMunicipalityDaneCode { get; set; } = string.Empty;

    /// <summary>Correo (máx. 150).</summary>
    public string IssuerEmail { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Motivo obligatorio (máx. 300).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
