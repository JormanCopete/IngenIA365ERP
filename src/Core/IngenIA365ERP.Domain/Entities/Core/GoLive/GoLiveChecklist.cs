using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.GoLive;

/// <summary>
/// <c>COR_GoLiveChecklists</c> (feature 015, E-06, T0033; R-28; FR-227; data-model §16.2): la lista de go/no-go de una salida en vivo de la
/// cooperativa, verificable en el sistema y no en una hoja aparte. Sirve a Recaudo, Tesorería, Cartera y Captaciones: cada módulo aporta
/// sus condiciones automáticas por <c>IVerificacionDeSalida</c> y las personas marcan las manuales con su evidencia. La decisión
/// (<see cref="GoLiveChecklistStatus.Go"/>, <c>NoGo</c>, <c>PartialGo</c>) se toma una vez y queda; un <c>Go</c> con una condición en rojo exige
/// que el ítem tenga su excepción aprobada (<c>Platform.GoLive.Override</c>) con motivo.
/// </summary>
public class GoLiveChecklist : AuditableEntity
{
    /// <summary>El nombre de la salida («Salida en vivo COOFLOPAL 01/12/2026»).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>El ambiente en que se verifica (<c>QA</c>, <c>Producción</c>…), en texto.</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>La fecha prevista de la salida.</summary>
    public DateOnly TargetDate { get; set; }

    /// <summary>Mutable: <c>Open</c> hasta la decisión; después no cambia.</summary>
    public GoLiveChecklistStatus Status { get; set; } = GoLiveChecklistStatus.Open;

    /// <summary>El <c>SEC_Users.Id</c> de quien decidió.</summary>
    public int? DecidedByUserId { get; set; }

    public string? DecidedByName { get; set; }

    /// <summary>Instante UTC de la decisión.</summary>
    public DateTime? DecisionAt { get; set; }

    /// <summary>El motivo de la decisión (obligatorio en <c>NoGo</c>, <c>PartialGo</c> y en un <c>Go</c> con rojos aprobados).</summary>
    public string? DecisionReason { get; set; }

    /// <summary>Con <c>PartialGo</c>: el escenario escrito de lo que no sale y el plan de reversa (P15).</summary>
    public Guid? ScenarioAttachmentPublicId { get; set; }

    public List<GoLiveChecklistItem> Items { get; set; } = [];
}

/// <summary>
/// <c>COR_GoLiveChecklistItems</c> (data-model §16.2): una condición de la lista. <see cref="Module"/> dice de quién es (<c>REC</c>,
/// <c>TES</c>, <c>CAR</c>, <c>Plataforma</c>) y <see cref="Code"/> cuál es. Una automática la vuelve a calcular el módulo; una manual la marca
/// una persona con <see cref="Evidence"/>. Seguir con una condición en rojo exige la aprobación registrada del dueño
/// (<see cref="OverrideApprovedByUserId"/>, <see cref="OverrideReason"/>): el rojo no se oculta, se acepta.
/// </summary>
public class GoLiveChecklistItem : AuditableEntity
{
    public int ChecklistId { get; set; }

    public GoLiveChecklist? Checklist { get; set; }

    /// <summary><c>REC</c>, <c>TES</c>, <c>CAR</c> o <c>Plataforma</c>.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>El código de la condición, único en la lista.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Qué se verifica, en palabras de la persona.</summary>
    public string Description { get; set; } = string.Empty;

    public GoLiveItemKind Kind { get; set; }

    /// <summary>Mutable mientras la lista esté abierta.</summary>
    public GoLiveItemStatus Status { get; set; } = GoLiveItemStatus.Red;

    /// <summary>Lo que respalda el estado: el detalle de la verificación automática o lo que anotó la persona.</summary>
    public string? Evidence { get; set; }

    /// <summary>Un soporte de la evidencia manual, si lo hay.</summary>
    public Guid? EvidenceAttachmentPublicId { get; set; }

    /// <summary>Instante UTC de la última verificación.</summary>
    public DateTime? CheckedAt { get; set; }

    public string? CheckedByName { get; set; }

    /// <summary>El <c>SEC_Users.Id</c> de quien aprobó seguir con la condición en rojo.</summary>
    public int? OverrideApprovedByUserId { get; set; }

    public string? OverrideApprovedByName { get; set; }

    public DateTime? OverrideApprovedAt { get; set; }

    /// <summary>Por qué se sigue con la condición en rojo.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>¿Está en rojo sin la excepción aprobada?</summary>
    public bool EnRojoSinExcepcion => Status == GoLiveItemStatus.Red && OverrideApprovedByUserId is null;
}
