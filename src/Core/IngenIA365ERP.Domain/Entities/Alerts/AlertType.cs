using System.Text.Json;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Alerts;

namespace IngenIA365ERP.Domain.Entities.Alerts;

/// <summary>
/// Una versión de la configuración de un tipo de alerta en la cooperativa (<c>COR_AlertTypes</c>; feature 012, T39,
/// T091; data-model §22). El <see cref="TypeCode"/> es del catálogo cerrado de decisiones-transversales §2.13: la
/// cooperativa configura destinatarios, canales y umbrales, no inventa tipos. Cada alta es una versión nueva que cierra
/// la anterior la víspera de <see cref="ValidFrom"/>. Escritores: <c>SaveAlertTypeCommandHandler</c> y la semilla
/// <c>AlertTypesSeeder</c>. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class AlertType : AuditableEntity
{
    /// <summary>Del catálogo cerrado (máx. 60).</summary>
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>Módulo de la bandeja y de la auditoría (máx. 20).</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>JSON con los códigos de permiso destinatarios (máx. 1000); ninguno de acción <c>View</c>.</summary>
    public string RecipientPermissions { get; set; } = "[]";

    public AlertChannels Channels { get; set; } = AlertChannels.InApp;

    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;

    /// <summary>Umbrales propios del tipo, JSON (máx. 2000); nulo si el tipo no tiene.</summary>
    public string? ThresholdsJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Motivo del cambio (máx. 300).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);

    /// <summary>Los permisos destinatarios leídos del JSON (vacío si el JSON no es una lista de textos).</summary>
    public IReadOnlyList<string> Permisos()
    {
        if (string.IsNullOrWhiteSpace(RecipientPermissions)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(RecipientPermissions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>La lista de permisos destinatarios como la guarda la columna.</summary>
    public static string PermisosComoJson(IEnumerable<string> permisos) => JsonSerializer.Serialize(permisos.ToList());
}
