using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Settings;

/// <summary>Una vigencia de la configuración de emisión (api.md §24.1). <see cref="CredentialKey"/> no viaja: se deriva. (nuevo)</summary>
public sealed record ElectronicEmissionSettingDto(
    Guid SettingPublicId,
    EmissionMode Mode,
    string ChannelCode,
    DianEnvironment Environment,
    string? SoftwareId,
    string? TestSetId,
    DateTime? TestSetAcceptedAt,
    EmailDeliveryBy EmailDeliveryBy,
    bool IsEnabled,
    DateTime? CredentialVerifiedAt,
    string IssuerTaxId,
    string IssuerCheckDigit,
    string IssuerBusinessName,
    string IssuerAddress,
    string IssuerMunicipalityDaneCode,
    string IssuerEmail,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? CreatedByName);

/// <summary>Las capacidades de un canal para la pantalla (api.md §24.1, <c>availableChannels[].capabilities</c>). (nuevo)</summary>
public sealed record CapacidadesDelCanalDto(
    IReadOnlyList<string> DocumentKinds,
    IReadOnlyList<string> Events,
    bool AcceptsErpNumber,
    bool IsAsync,
    bool ReturnsPdf,
    bool CanSendEmail,
    bool QueriesNumberingRanges,
    bool IssuerContingency);

/// <summary>Un canal disponible en la instalación. (nuevo)</summary>
public sealed record CanalDisponibleDto(string ChannelCode, string Name, CapacidadesDelCanalDto Capabilities);

/// <summary>El estado de la credencial del canal vigente: la clave derivada, si está en el secreto y cuándo se verificó. (nuevo)</summary>
public sealed record CredencialDelCanalDto(string? Key, bool Configured, DateTime? VerifiedAt);

/// <summary>La respuesta de <c>GET /api/electronic-invoicing/settings</c> (api.md §24.1). (nuevo)</summary>
public sealed record EmissionSettingsDto(
    ElectronicEmissionSettingDto? Current,
    IReadOnlyList<ElectronicEmissionSettingDto> History,
    IReadOnlyList<CanalDisponibleDto> AvailableChannels,
    CredencialDelCanalDto Credential);

/// <summary>Piezas compartidas de la configuración de emisión (T708). (nuevo)</summary>
public static class ConfiguracionDeEmision
{
    public const string TenantNotSelectedCode = "Session.TenantNotSelected";

    /// <summary>El <c>PublicId</c> de la cooperativa resuelta (ICurrentTenantService lleva el PublicId, no el Id interno).</summary>
    public static Result<Guid> Cooperativa(ICurrentTenantService tenant) =>
        Guid.TryParse(tenant.TenantId, out var publica) && publica != Guid.Empty
            ? Result.Success(publica)
            : Result.Failure<Guid>(TenantNotSelectedCode, "Hace falta una cooperativa activa para configurar la facturación electrónica.");

    public static ElectronicEmissionSettingDto ADto(ElectronicEmissionSetting s) => new(
        s.PublicId, s.Mode, s.ChannelCode, s.Environment, s.SoftwareId, s.TestSetId, s.TestSetAcceptedAt, s.EmailDeliveryBy, s.IsEnabled,
        s.CredentialVerifiedAt, s.IssuerTaxId, s.IssuerCheckDigit, s.IssuerBusinessName, s.IssuerAddress, s.IssuerMunicipalityDaneCode,
        s.IssuerEmail, s.ValidFrom, s.ValidTo, s.Reason, s.CreatedBy);

    public static CanalDisponibleDto Canal(ICanalDeEmisionElectronica canal)
    {
        var c = canal.Capacidades;
        var documentos = c.Tipos.Where(t => t is not (ElectronicDocumentKind.RadianEvent030 or ElectronicDocumentKind.RadianEvent032))
            .OrderBy(t => t).Select(t => t.ToString()).ToList();
        var eventos = c.Tipos.Where(t => t is ElectronicDocumentKind.RadianEvent030 or ElectronicDocumentKind.RadianEvent032)
            .OrderBy(t => t).Select(t => t.ToString()).ToList();
        var nombre = string.Equals(canal.ChannelCode, GuardiaDeEmisionFiscal.CanalSimulado, StringComparison.OrdinalIgnoreCase)
            ? "Canal simulado (sólo pruebas)"
            : canal.ChannelCode;
        return new CanalDisponibleDto(canal.ChannelCode, nombre,
            new CapacidadesDelCanalDto(documentos, eventos, c.AceptaNumeroDelErp, c.EsAsincrono, c.DevuelvePdf, c.PuedeEnviarCorreo,
                c.ConsultaRangos, c.ContingenciaDelFacturador));
    }
}
