using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;

namespace IngenIA365ERP.Domain.Entities.Lending.Governance;

/// <summary>
/// <c>LND_SessionVotes</c> (feature 013, T0213; data-model §3.11; FR-039, US2-6, US2-7): el voto de una persona sobre un punto. Hecho
/// (<see cref="IHechoInmutable"/>): un voto mal registrado se corrige, con la sesión abierta, con un voto de reemplazo que lo
/// referencia (<see cref="ReplacesVoteId"/>), y el mismo guardado escribe <see cref="ReplacedAt"/> en el anterior (C75).
///
/// <para>
/// T0282: <see cref="Registrar"/> exige la sesión abierta (<c>Lending.Session.NotOpen</c> / <c>.Closed</c>), el punto de esa sesión
/// y que la persona no tenga ya un voto vigente en el punto (<c>Lending.Session.VoteDuplicate</c>); <see cref="Reemplazar"/> exige
/// motivo (lo exige el validador), la sesión abierta y un voto todavía vigente, y marca <see cref="ReplacedAt"/> en el anterior (la única escritura que el
/// hecho admite). Quién puede votar —miembro con voto, presente, no excluido— lo decide el comando con
/// <see cref="EvaluadorDeVotacion"/>.
/// </para>
/// </summary>
public class SessionVote : AuditableEntity, IHechoInmutable
{
    public const string CodigoDuplicado = "Lending.Session.VoteDuplicate";

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

    /// <summary>Cuenta: nadie lo reemplazó.</summary>
    public bool EsVigente => ReplacedAt is null;

    /// <summary>Lo que impide registrar el voto, o nulo.</summary>
    public static ProblemaDeCartera? PuedeRegistrar(Session sesion, SessionItem punto, int personId, IEnumerable<SessionVote> votosDelPunto)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(punto);
        if (punto.SessionId != sesion.Id) throw new ArgumentException("El punto es de otra sesión.", nameof(punto));
        if (sesion.ExigirAbierta() is { } noAbierta) return noAbierta;
        return votosDelPunto.Any(v => v.ItemId == punto.Id && v.PersonId == personId && v.EsVigente)
            ? new ProblemaDeCartera(CodigoDuplicado, "La persona ya votó en este punto. Si el voto quedó mal registrado, reemplácelo con motivo.")
            : null;
    }

    /// <summary>Registra el voto (lanza si no se preguntó antes con <see cref="PuedeRegistrar"/>).</summary>
    public static SessionVote Registrar(
        Session sesion, SessionItem punto, int personId, int memberId, VoteValue value, VoteMethod method, int recordedByUserId,
        DateTime recordedAt, IEnumerable<SessionVote> votosDelPunto, Guid? presenceChallengePublicId = null)
    {
        ReglaDeCarteraException.SiHay(PuedeRegistrar(sesion, punto, personId, votosDelPunto));
        ExigirPresencia(method, presenceChallengePublicId);
        return new SessionVote
        {
            ItemId = punto.Id,
            PersonId = personId,
            MemberId = memberId,
            Value = value,
            Method = method,
            PresenceChallengePublicId = presenceChallengePublicId,
            RecordedByUserId = recordedByUserId,
            RecordedAt = recordedAt,
        };
    }

    /// <summary>
    /// El voto que corrige a <paramref name="anterior"/> (C75): misma persona y punto, con motivo; marca <see cref="ReplacedAt"/> en el
    /// anterior para que el mismo guardado escriba los dos.
    /// </summary>
    public static SessionVote Reemplazar(
        Session sesion, SessionVote anterior, VoteValue value, string reason, VoteMethod method, int recordedByUserId, DateTime recordedAt,
        Guid? presenceChallengePublicId = null)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(anterior);
        ReglaDeCarteraException.SiHay(sesion.ExigirAbierta());
        if (!anterior.EsVigente || anterior.Id <= 0)
            throw new InvalidOperationException("Sólo se reemplaza un voto registrado y vigente: el comando lo busca entre los votos vigentes del punto.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reemplazar un voto exige el motivo de la corrección (lo exige el validador del comando).", nameof(reason));
        ExigirPresencia(method, presenceChallengePublicId);

        anterior.ReplacedAt = recordedAt;
        return new SessionVote
        {
            ItemId = anterior.ItemId,
            PersonId = anterior.PersonId,
            MemberId = anterior.MemberId,
            Value = value,
            Method = method,
            PresenceChallengePublicId = presenceChallengePublicId,
            RecordedByUserId = recordedByUserId,
            RecordedAt = recordedAt,
            ReplacesVoteId = anterior.Id,
            ReplacementReason = reason.Trim(),
        };
    }

    /// <summary>El voto como lo cuenta <see cref="EvaluadorDeVotacion"/> (el reemplazado también: el motor cuenta el vigente).</summary>
    public VotoRegistrado ComoVoto() => new(Id, PersonId, Value, ReplacesVoteId);

    private static void ExigirPresencia(VoteMethod method, Guid? desafio)
    {
        var presencial = method is VoteMethod.InPersonPasskey or VoteMethod.InPersonTotp;
        if (presencial != desafio.HasValue)
            throw new ArgumentException(presencial
                ? "Un voto presencial lleva el desafío de presencia que lo respalda."
                : "Sólo un voto presencial lleva desafío de presencia.", nameof(desafio));
    }
}
