using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_SessionAttendances</c> (feature 013, T0213; data-model §3.9; FR-039): la asistencia de una persona a la sesión, miembro o
/// invitado (<see cref="MemberId"/> nulo). Se edita mientras la sesión no esté cerrada; no es hecho.
/// </summary>
public class SessionAttendance : AuditableEntity
{
    public int SessionId { get; set; }

    public int PersonId { get; set; }

    /// <summary>Nulo = invitado.</summary>
    public int? MemberId { get; set; }

    public AttendanceStatus Status { get; set; }

    /// <summary>Principal por el que asiste el suplente.</summary>
    public int? SubstitutesMemberId { get; set; }

    public DateTime? ArrivedAt { get; set; }

    public DateTime? LeftAt { get; set; }
}
