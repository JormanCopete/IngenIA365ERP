using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Lending.Segregation;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_CaseParticipations</c> (feature 013, T0213; data-model §3.13; FR-005, T66): registro único de intervenciones para la
/// segregación de funciones. Lo escribe sólo <c>RegistroDeIntervenciones</c>, en la misma transacción del acto. Hecho de sólo
/// inserción (<see cref="IHechoInmutable"/>).
///
/// <para>
/// T0283: nace por <see cref="Registrar"/>, que exige una acción de <see cref="AccionesDeSegregacion"/>, el usuario y la persona
/// (<c>Lending.Security.UserWithoutPerson</c> lo responde el registro antes de llegar aquí). <see cref="ComoPrevia"/> la entrega a
/// <see cref="ReglasDeSegregacion"/>.
/// </para>
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

    /// <summary>La intervención de <paramref name="userId"/> (persona <paramref name="personId"/>) sobre un sujeto del expediente.</summary>
    public static CaseParticipation Registrar(
        string caseRootType, Guid caseRootPublicId, string subjectType, Guid subjectPublicId, string action, int userId, int personId, DateTime at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseRootType);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        if (caseRootPublicId == Guid.Empty || subjectPublicId == Guid.Empty)
            throw new ArgumentException("El expediente y el sujeto se identifican por su PublicId.");
        if (!AccionesDeSegregacion.Existe(action))
            throw new ArgumentException($"«{action}» no es una acción de segregación de Cartera.", nameof(action));
        ArgumentOutOfRangeException.ThrowIfLessThan(userId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(personId, 1);
        return new CaseParticipation
        {
            CaseRootType = caseRootType,
            CaseRootPublicId = caseRootPublicId,
            SubjectType = subjectType,
            SubjectPublicId = subjectPublicId,
            Action = action,
            UserId = userId,
            PersonId = personId,
            At = at,
        };
    }

    /// <summary>La intervención como la compara <see cref="ReglasDeSegregacion"/>.</summary>
    public IntervencionPrevia ComoPrevia() => new(CaseRootPublicId, Action, UserId, PersonId, At);
}
