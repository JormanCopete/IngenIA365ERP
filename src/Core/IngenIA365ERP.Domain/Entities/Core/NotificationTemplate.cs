using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_NotificationTemplates] (nueva — email/SMS/push templates).
///
/// <para>
/// Feature 013 (T0084; T76, F-07; data-model parte 3 §4.12; C68, C90): la tabla existía vacía en cada cooperativa y se adopta como
/// <b>plantilla de mensaje versionada</b> con columnas aditivas. Una plantilla es (<see cref="Code"/>, <see cref="Channel"/>,
/// <see cref="Version"/>): cambiarla crea una fila nueva con <c>Version + 1</c> y cierra la anterior la víspera
/// (<c>CreateMessageTemplateVersionCommand</c>); el mensaje al deudor sella <c>PublicId</c> y versión, así lo que ya salió nunca cambia.
/// <see cref="Channel"/> toma el texto de <c>MessageChannel</c> (<c>Email</c>, <c>Sms</c>, <c>WhatsApp</c>, <c>Letter</c>) y
/// <see cref="Purpose"/> el de <c>DebtorMessagePurpose</c>.
/// </para>
/// </summary>
public class NotificationTemplate : AuditableEntity
{
    [MaxLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Channel { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Subject { get; set; }

    public string BodyTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Código de la plantilla con las reglas de <c>CodigoDeCatalogo</c> (10 caracteres, p. ej. <c>AVISOPREV</c>; C90).</summary>
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    /// <summary>El módulo dueño (<c>Lending</c>).</summary>
    [MaxLength(20)]
    public string Module { get; set; } = "Lending";

    /// <summary>El propósito (texto de <c>DebtorMessagePurpose</c>): decide la lista blanca de variables.</summary>
    [MaxLength(30)]
    public string Purpose { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DateOnly ValidFrom { get; set; }

    /// <summary>Lo escribe la versión siguiente (su víspera).</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>La plantilla aprobada de WhatsApp que la cooperativa registró ante su proveedor; sin ella no se usa WhatsApp (pregunta I15).</summary>
    [MaxLength(100)]
    public string? ProviderTemplateId { get; set; }

    /// <summary>JSON con la lista blanca de variables del propósito (la hoja de ayuda de la pantalla sale de aquí).</summary>
    [MaxLength(2000)]
    public string Variables { get; set; } = "[]";

    /// <summary>La sembró la plataforma.</summary>
    public bool IsSeeded { get; set; }

    /// <summary>«Pendiente de validar»: la sembrada que nadie ha revisado (precedente <c>TaxRate.ReviewPending</c>).</summary>
    public bool ReviewPending { get; set; }

    // Navigation properties
    public ICollection<Notification> Notifications { get; set; } = [];

    /// <summary>¿Rige en <paramref name="fecha"/>?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
