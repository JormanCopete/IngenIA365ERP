using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_SessionAttendances</c> (feature 013, T0213; data-model §3.9; FR-039): la asistencia de una persona a la sesión, miembro o
/// invitado (<see cref="MemberId"/> nulo). Se edita mientras la sesión no esté cerrada; no es hecho.
///
/// <para>
/// T0282: <see cref="Registrar"/> y <see cref="Cambiar"/> exigen la sesión no cerrada (<c>Lending.Session.Closed</c>); un suplente
/// asiste por un principal sólo si es miembro (<c>Lending.Session.SubstituteInvalid</c>).
/// </para>
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

    public const string CodigoSuplenteInvalido = "Lending.Session.SubstituteInvalid";

    /// <summary>La asistencia de una persona a la sesión (abierta o en borrador).</summary>
    public static SessionAttendance Registrar(Session sesion, int personId, int? memberId, AttendanceStatus status,
        int? substitutesMemberId = null, DateTime? arrivedAt = null)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ReglaDeCarteraException.SiHay(sesion.ExigirEditable());
        ReglaDeCarteraException.SiHay(ValidarSuplencia(memberId, substitutesMemberId));
        return new SessionAttendance
        {
            SessionId = sesion.Id,
            PersonId = personId,
            MemberId = memberId,
            Status = status,
            SubstitutesMemberId = substitutesMemberId,
            ArrivedAt = status == AttendanceStatus.Present ? arrivedAt : null,
        };
    }

    /// <summary>Corrige la asistencia mientras la sesión no esté cerrada.</summary>
    public void Cambiar(Session sesion, AttendanceStatus status, int? substitutesMemberId = null, DateTime? arrivedAt = null, DateTime? leftAt = null)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        if (sesion.Id != SessionId) throw new ArgumentException("La asistencia es de otra sesión.", nameof(sesion));
        ReglaDeCarteraException.SiHay(sesion.ExigirEditable());
        ReglaDeCarteraException.SiHay(ValidarSuplencia(MemberId, substitutesMemberId));
        Status = status;
        SubstitutesMemberId = substitutesMemberId;
        ArrivedAt = status == AttendanceStatus.Present ? arrivedAt ?? ArrivedAt : null;
        LeftAt = leftAt;
    }

    /// <summary>La asistencia como la cuenta <see cref="EvaluadorDeVotacion"/>.</summary>
    public AsistenciaASesion ComoAsistencia() => new(PersonId, Status);

    private static ProblemaDeCartera? ValidarSuplencia(int? memberId, int? substitutesMemberId) =>
        substitutesMemberId is not null && (memberId is null || memberId == substitutesMemberId)
            ? new ProblemaDeCartera(CodigoSuplenteInvalido, "Sólo un miembro suplente asiste por un principal, y por otro que no es él mismo.")
            : null;
}
