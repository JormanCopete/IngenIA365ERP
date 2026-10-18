using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_CollectorAssignments</c> (feature 013, K4, T0936; data-model §26.6; FR-118): la asignación de un crédito —o del deudor completo,
/// con el crédito nulo— a un gestor, por una regla o a mano. La vigente es la última; reasignar es otra fila que apunta a la anterior. Una
/// asignación manual o una reasignación llevan motivo; la manual, además, a la persona (<c>Lending.Collections.Assign</c>). Hecho de sólo
/// inserción (<see cref="IHechoInmutable"/>). Una fila <b>sin gestor</b> cierra la asignación del crédito (K4, bloque D: el crédito salió de cobranza)
/// y lo deja sin gestor vigente hasta que se le asigne otro. (nuevo)
/// </summary>
public class CollectorAssignment : AuditableEntity, IHechoInmutable
{
    /// <summary>Nulo = el deudor completo.</summary>
    public int? LoanId { get; init; }

    public int PersonId { get; init; }

    /// <summary>El gestor; nulo = la asignación del crédito se cerró (<see cref="Cerrar"/>).</summary>
    public int? CollectorUserId { get; init; }

    /// <summary>¿Es el cierre de la asignación (sin gestor)?</summary>
    public bool EsCierre => CollectorUserId is null;

    public DateOnly AssignedOn { get; init; }

    /// <summary>Nula = asignación manual.</summary>
    public int? CollectorAssignmentRuleId { get; init; }

    public string? Reason { get; init; }

    /// <summary>Nulo = el proceso.</summary>
    public int? ActorUserId { get; init; }

    public int? PreviousAssignmentId { get; init; }

    public bool EsManual => CollectorAssignmentRuleId is null && !EsCierre;

    public static CollectorAssignment Asignar(int? creditoId, int personaId, int gestorId, DateOnly dia, int? reglaId, string? motivo, int? usuarioId,
        int? anteriorId = null)
    {
        if (reglaId is null && (usuarioId is null || string.IsNullOrWhiteSpace(motivo)))
            throw new ArgumentException("Una asignación manual lleva la persona que la hace y el motivo.", nameof(motivo));
        if (anteriorId is not null && string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Una reasignación lleva motivo.", nameof(motivo));
        return new CollectorAssignment
        {
            LoanId = creditoId,
            PersonId = personaId,
            CollectorUserId = gestorId,
            AssignedOn = dia,
            CollectorAssignmentRuleId = reglaId,
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            ActorUserId = usuarioId,
            PreviousAssignmentId = anteriorId,
        };
    }

    /// <summary>
    /// Cierra la asignación vigente de un crédito (K4, bloque D): una fila del crédito sin gestor, con el motivo y la anterior enlazada. La escribe el
    /// proceso (sin persona) cuando el crédito sale de cobranza.
    /// </summary>
    public static CollectorAssignment Cerrar(int creditoId, int personaId, DateOnly dia, string motivo, int? anteriorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        return new CollectorAssignment
        {
            LoanId = creditoId,
            PersonId = personaId,
            CollectorUserId = null,
            AssignedOn = dia,
            Reason = motivo.Trim(),
            PreviousAssignmentId = anteriorId,
        };
    }
}
