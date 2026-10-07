using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_SessionVotes</c> (feature 013, T0213; data-model §3.11; FR-039, US2-6, US2-7): el voto de una persona sobre un punto. Hecho
/// (<see cref="IHechoInmutable"/>): un voto mal registrado se corrige, con la sesión abierta, con un voto de reemplazo que lo
/// referencia (<see cref="ReplacesVoteId"/>), y el mismo guardado escribe <see cref="ReplacedAt"/> en el anterior (C75).
/// </summary>
public class SessionVote : AuditableEntity, IHechoInmutable
{
    public int ItemId { get; init; }

    public int PersonId { get; init; }

    public int MemberId { get; init; }

    public VoteValue Value { get; init; }

    public VoteMethod Method { get; init; } = VoteMethod.RecordedBySecretary;

    /// <summary>Desafío de <c>IDesafiosDePresencia</c> cuando el voto es presencial.</summary>
    public Guid? PresenceChallengePublicId { get; init; }

    public int RecordedByUserId { get; init; }

    public DateTime RecordedAt { get; init; }

    /// <summary>El voto mal registrado que éste reemplaza.</summary>
    public int? ReplacesVoteId { get; init; }

    public string? ReplacementReason { get; init; }

    /// <summary>Lo escribe, en el voto anterior, el mismo guardado que inserta su reemplazo.</summary>
    [EscrituraUnica]
    public DateTime? ReplacedAt { get; set; }
}
