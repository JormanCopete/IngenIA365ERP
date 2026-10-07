using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_CaseParticipations</c> (feature 013, T0213; data-model §3.13; FR-005, T66): registro único de intervenciones para la
/// segregación de funciones. Lo escribe sólo <c>RegistroDeIntervenciones</c>, en la misma transacción del acto. Hecho de sólo
/// inserción (<see cref="IHechoInmutable"/>).
/// </summary>
public class CaseParticipation : AuditableEntity, IHechoInmutable
{
    /// <summary>Clase del expediente: la solicitud, el crédito sin solicitud, la novedad, la condonación, el lote de castigo, la carga.</summary>
    public string CaseRootType { get; init; } = string.Empty;

    public Guid CaseRootPublicId { get; init; }

    /// <summary>Sobre qué actuó.</summary>
    public string SubjectType { get; init; } = string.Empty;

    public Guid SubjectPublicId { get; init; }

    /// <summary>Token de <c>AccionesDeSegregacion</c> (<c>Radicar</c>, <c>Decidir</c>, <c>Desembolsar</c>…).</summary>
    public string Action { get; init; } = string.Empty;

    public int UserId { get; init; }

    /// <summary>Obligatorio (<c>Lending.Security.UserWithoutPerson</c>).</summary>
    public int PersonId { get; init; }

    public DateTime At { get; init; }
}
