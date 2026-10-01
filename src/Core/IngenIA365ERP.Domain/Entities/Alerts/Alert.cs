using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Alerts;

/// <summary>
/// Una alerta levantada (<c>COR_Alerts</c>; feature 012, T39, T091; data-model §22). Su estado es <b>compartido</b>: la
/// atiende una persona, con nota, y queda atendida para todos, que es lo que <c>COR_Notifications</c> (una fila por
/// destinatario) no modela. Mientras está pendiente, la misma condición (<see cref="DedupKey"/>) no levanta otra: suma
/// en <see cref="OccurrenceCount"/>. Escritores: <c>IAlertas</c> (levantar y atender por proceso) y
/// <c>AttendAlertCommandHandler</c>. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class Alert : AuditableEntityLong
{
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>La versión de <see cref="AlertType"/> vigente al levantarla.</summary>
    public int AlertTypeId { get; set; }

    public AlertType? AlertType { get; set; }

    public string Module { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>Qué pasó y qué hacer (FR-020).</summary>
    public string Body { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public Guid? EntityPublicId { get; set; }

    /// <summary>Sólo la reciben y la ven quienes tienen alcance sobre esta bodega.</summary>
    public Guid? ScopeWarehousePublicId { get; set; }

    /// <summary>Sólo la reciben y la ven quienes tienen alcance sobre este punto de venta.</summary>
    public Guid? ScopePointOfSalePublicId { get; set; }

    /// <summary><c>{TypeCode}:{entidad}[:{bodega}]</c> (máx. 200); única mientras la alerta está pendiente.</summary>
    public string DedupKey { get; set; } = string.Empty;

    public AlertStatus Status { get; set; } = AlertStatus.Pending;

    public DateTime RaisedAt { get; set; }

    public ActorKind RaisedByKind { get; set; }

    public string RaisedByName { get; set; } = string.Empty;

    public int OccurrenceCount { get; set; } = 1;

    public DateTime LastOccurredAt { get; set; }

    public DateTime? AttendedAt { get; set; }

    /// <summary><c>Process</c> cuando se cierra sola porque desapareció la causa.</summary>
    public ActorKind? AttendedByKind { get; set; }

    public int? AttendedByUserId { get; set; }

    public string? AttendedByName { get; set; }

    public string? AttendNote { get; set; }

    public int RecipientCount { get; set; }

    /// <summary>Nadie activo tenía el permiso: se enrutó a los titulares de <c>CompanyAdmin</c> (SC-022).</summary>
    public bool WithoutRecipient { get; set; }

    /// <summary>Una repetición de la misma condición mientras sigue pendiente.</summary>
    public void Repetir(DateTime ahora)
    {
        OccurrenceCount++;
        LastOccurredAt = ahora;
    }

    /// <summary>
    /// La deja atendida para todos, con quién, cuándo y la nota. Atender no corrige la causa: si sigue, la próxima
    /// revisión levanta otra (la <see cref="DedupKey"/> ya no está pendiente).
    /// </summary>
    /// <exception cref="InvalidOperationException">Si ya estaba atendida: quien llama lo responde antes.</exception>
    public void Atender(ActorKind kind, int? userId, string nombre, string nota, DateTime ahora)
    {
        if (Status == AlertStatus.Attended)
            throw new InvalidOperationException($"La alerta {PublicId} ya fue atendida por {AttendedByName} el {AttendedAt:O}.");
        Status = AlertStatus.Attended;
        AttendedAt = ahora;
        AttendedByKind = kind;
        AttendedByUserId = userId;
        AttendedByName = nombre;
        AttendNote = nota;
    }
}
