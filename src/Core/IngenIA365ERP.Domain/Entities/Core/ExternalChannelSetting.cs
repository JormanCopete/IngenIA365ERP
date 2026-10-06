using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ExternalChannels;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// El canal externo de la cooperativa para una clase, con su vigencia (<c>COR_ExternalChannelSettings</c>; feature 013, T0080; T72, F-02;
/// data-model parte 3 §4.7; contracts/adaptadores-y-archivos.md §3.3). Guarda <b>sólo</b> la referencia a la credencial
/// (<see cref="CredentialKey"/>): la credencial vive en el Secret <c>erp-canales-externos</c>
/// (<c>LasCredencialesDeCanalesExternosNoTocanLaBase</c>). Esta tabla decide el canal de las operaciones <b>nuevas</b>; cada operación sella
/// el suyo y lo sigue usando aunque la configuración cambie. Configurar otro canal cierra la fila vigente la víspera
/// (<c>ConfigureExternalChannelCommand</c>). <c>SIMULADO</c> se admite en producción antes y después de activar: la guardia le quita todo
/// efecto legal (T73). (nuevo)
/// </summary>
public class ExternalChannelSetting : AuditableEntity
{
    public ExternalChannelKind Kind { get; set; }

    /// <summary>Código de la central (<c>DATACREDITO</c>, <c>TRANSUNION</c>…); sólo con <see cref="ExternalChannelKind.CreditBureau"/>.</summary>
    public string? BureauCode { get; set; }

    /// <summary><c>{Kind}|{BureauCode|*}</c>: la clave cuyas vigencias no se cruzan (fijado en data-model §4.7).</summary>
    public string SettingKey { get; set; } = string.Empty;

    /// <summary><c>SIMULADO</c>, <c>CARGA-PROPIA</c> (sólo listas) o el código del adaptador del proveedor; también arma la ruta de la credencial.</summary>
    public string ChannelCode { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; set; }

    /// <summary>Lo cierra la configuración siguiente.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>
    /// El nombre de la clave del Secret (<c>{tenantPublicId}.{channelCode}.json</c>), nunca el secreto. Nulo en <c>SIMULADO</c> y
    /// <c>CARGA-PROPIA</c>. No viaja al diff de la auditoría.
    /// </summary>
    [NoAuditar]
    public string? CredentialKey { get; set; }

    /// <summary>El último <c>ProbarAsync</c> que respondió <c>Ok</c>.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary><c>SEC_Users.Id</c> de quien verificó por última vez.</summary>
    public int? VerifiedByUserId { get; set; }

    public string? VerifiedByName { get; set; }

    public ExternalOutcome? LastVerificationOutcome { get; set; }

    public string? LastVerificationMessage { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>La clave de las vigencias: <c>{Kind}|{BureauCode|*}</c>.</summary>
    public static string ClaveDe(ExternalChannelKind kind, string? bureauCode) =>
        $"{kind}|{(string.IsNullOrWhiteSpace(bureauCode) ? "*" : bureauCode.Trim().ToUpperInvariant())}";

    /// <summary>¿Rige en <paramref name="fecha"/>?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
