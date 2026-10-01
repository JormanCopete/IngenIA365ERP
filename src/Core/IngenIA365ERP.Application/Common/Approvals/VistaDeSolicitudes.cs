using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// Lo que el motor y las consultas de aprobaciones comparten (feature 012, T083, T085; nuevo): quién puede decidir
/// una solicitud y por qué no, si está al alcance, qué fuente la atiende y cómo se muestra en la bandeja. La
/// segregación compara <c>SEC_Users.Id</c> de <c>IActorActual</c>, nunca el entero del token ni el correo.
/// </summary>
public sealed class VistaDeSolicitudes(IApplicationDbContext db, IEnumerable<IFuenteDeAprobacion> fuentes)
{
    private readonly IReadOnlyDictionary<string, IFuenteDeAprobacion> _fuentes =
        fuentes.GroupBy(f => f.SourceType, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

    /// <summary>La fuente registrada para el <c>SourceType</c>, o nula.</summary>
    public IFuenteDeAprobacion? FuenteDe(string sourceType) => _fuentes.GetValueOrDefault(sourceType);

    /// <summary>
    /// Si la solicitud está al alcance: sin bodega ni punto, sí; con alcance total de lo que tiene, sí; si no, lo dice
    /// su fuente, y sin fuente, no (falla cerrado).
    /// </summary>
    public async Task<bool> AlAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        var bodegaCubierta = solicitud.ScopeWarehousePublicId is null || alcance.TodasLasBodegas;
        var puntoCubierto = solicitud.ScopePointOfSalePublicId is null || alcance.TodosLosPuntos;
        if (bodegaCubierta && puntoCubierto) return true;

        return FuenteDe(solicitud.SourceType) is { } fuente && await fuente.EnAlcanceAsync(solicitud, alcance, ct);
    }

    /// <summary>Creador, solicitante, participantes declarados y quienes ya aprobaron un nivel.</summary>
    public static ParticipantesDeAprobacion ParticipantesDe(ApprovalRequest solicitud)
    {
        var previos = solicitud.Decisions
            .Where(d => d.Decision == ApprovalDecisionKind.Approve && !d.IsDeleted)
            .Select(d => d.DecidedByUserId)
            .ToHashSet();
        var participantes = solicitud.Excluidos()
            .Where(id => id != solicitud.CreatedByUserId && id != solicitud.RequestedByUserId && !previos.Contains(id))
            .ToList();
        return new ParticipantesDeAprobacion(solicitud.CreatedByUserId, solicitud.RequestedByUserId, participantes, previos);
    }

    /// <summary>El nivel actual de una solicitud (el sellado cuyo orden es <c>CurrentLevel</c>).</summary>
    public static NivelDeAprobacion? NivelActual(ApprovalRequest solicitud) =>
        solicitud.NivelesRequeridos().FirstOrDefault(n => n.Order == solicitud.CurrentLevel);

    /// <summary>
    /// Arma los DTO de la bandeja: la fuente describe lo aprobado (o se usa lo que la solicitud guarda), los niveles con
    /// su estado, los nombres de quien creó y pidió, y si <paramref name="userId"/> puede decidir ahora.
    /// </summary>
    /// <param name="puedeDecidir">Por solicitud: si quien consulta tiene el permiso del nivel y alcance.</param>
    public async Task<IReadOnlyList<ApprovalRequestDto>> ADtosAsync(
        IReadOnlyList<ApprovalRequest> solicitudes,
        int? userId,
        Func<ApprovalRequest, Task<bool>> tienePermisoYAlcance,
        bool conDecisiones,
        CancellationToken ct)
    {
        if (solicitudes.Count == 0) return [];

        var descritas = new Dictionary<Guid, OrigenDeAprobacionDto>();
        foreach (var grupo in solicitudes.GroupBy(s => s.SourceType))
        {
            if (FuenteDe(grupo.Key) is not { } fuente) continue;
            foreach (var (id, origen) in await fuente.DescribirAsync(grupo.Select(s => s.SourcePublicId).Distinct().ToList(), ct))
                descritas[id] = origen;
        }

        var ids = solicitudes.SelectMany(s => new[] { s.CreatedByUserId, s.RequestedByUserId }).Distinct().ToList();
        var nombres = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, Nombre = u.Email ?? u.Username })
            .ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);

        var dtos = new List<ApprovalRequestDto>(solicitudes.Count);
        foreach (var s in solicitudes)
        {
            var decisiones = s.Decisions.Where(d => !d.IsDeleted).OrderBy(d => d.DecidedAt).ToList();
            var niveles = s.NivelesRequeridos().Select(n =>
            {
                var decision = decisiones.LastOrDefault(d => d.Level == n.Order);
                var estado = decision is not null
                    ? (decision.Decision == ApprovalDecisionKind.Approve ? "Approved" : "Rejected")
                    : s.Status == ApprovalRequestStatus.Pending && n.Order == s.CurrentLevel ? "Pending" : "Waiting";
                return new NivelDeSolicitudDto(n.Order, n.Threshold, n.PermissionCode, estado,
                    decision?.DecidedByName, decision?.DecidedAt, decision?.Method.ToString());
            }).ToList();

            MotivoDeExclusion? excluido = null;
            var puede = false;
            if (s.Status == ApprovalRequestStatus.Pending && userId is { } yo)
            {
                excluido = EvaluadorDePolitica.ValidarDecision(yo, ParticipantesDe(s));
                puede = excluido is null && await tienePermisoYAlcance(s);
            }

            var origen = descritas.GetValueOrDefault(s.SourcePublicId)
                ?? new OrigenDeAprobacionDto(s.SourcePublicId, null, null, s.SourceLabel, s.OperationDate,
                    s.ScopeWarehousePublicId, s.ScopePointOfSalePublicId, s.SourceLabel, null);

            dtos.Add(new ApprovalRequestDto(
                s.PublicId, s.Module, s.Subject, s.SourceType, origen, s.Amount, s.Currency, s.Status.ToString(),
                s.CurrentLevel, niveles, nombres.GetValueOrDefault(s.CreatedByUserId), nombres.GetValueOrDefault(s.RequestedByUserId),
                s.RequestedAt, s.ContentSha256, puede, excluido?.ToString(),
                conDecisiones
                    ? decisiones.Select(d => new DecisionDto(d.Level, d.Decision.ToString(), d.DecidedByName, d.DecidedAt, d.Reason, d.Method.ToString())).ToList()
                    : null));
        }

        return dtos;
    }
}
