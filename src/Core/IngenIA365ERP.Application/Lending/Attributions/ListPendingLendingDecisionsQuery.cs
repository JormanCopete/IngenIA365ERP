using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Attributions;

/// <summary>Un asunto que espera decisión (api.md §4.3, «Mis pendientes»). (nuevo)</summary>
public sealed record PendingLendingDecisionDto(
    string Subject,
    Guid SubjectPublicId,
    string? Number,
    string Summary,
    decimal Amount,
    string LevelCode,
    string LevelName,
    DateTime? Since,
    string Route,
    string? GoverningBody,
    bool CanDecide,
    string? ExcludedReason);

/// <summary>Las rutas de un pendiente: lo decide una persona o va a sesión de un órgano. (nuevo)</summary>
public static class RutasDePendiente
{
    public const string Individual = "Individual";
    public const string Sesion = "Session";

    public const string ExcluidoPorRadicar = "Filer";
    public const string ExcluidoPorSegregacion = "Segregation";
    public const string ExcluidoSinPersona = "UserWithoutPerson";
    public const string ExcluidoPorSerParte = "Applicant";
}

/// <summary>
/// «Mis pendientes» (api.md §4.3, <c>GET /api/lending/pending-decisions?subject=&amp;page=</c>, <c>Lending.Applications.Read</c>; T65,
/// <c>/cartera/aprobaciones</c>): lo que el usuario puede decidir ahora —tiene el permiso del nivel o uno mayor (<see cref="AtribucionDelDecisor"/>),
/// el asunto está en su alcance de oficina y línea— con <c>canDecide</c> y, si la segregación lo excluye (radicó, intervino o no tiene persona),
/// <c>excludedReason</c>; y lo que está en la agenda de sus órganos (niveles colegiados de órganos donde es miembro con voto vigente), que va a
/// sesión. Solicitudes que esperan decisión (también las aprobadas y vencidas, C53) y cupos que esperan decisión. Los más antiguos primero. (nuevo)
/// </summary>
public sealed record ListPendingLendingDecisionsQuery(string? Subject = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<PendingLendingDecisionDto>>>;

public sealed class ListPendingLendingDecisionsQueryValidator : AbstractValidator<ListPendingLendingDecisionsQuery>
{
    public ListPendingLendingDecisionsQueryValidator() =>
        RuleFor(x => x.Subject).Must(s => s is null || LendingDecisionSubjects.Existe(s)).WithMessage("Elija un asunto que se decida en Cartera.");
}

public sealed class ListPendingLendingDecisionsQueryHandler(
    IApplicationDbContext db,
    AtribucionDelDecisor atribucion,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ListPendingLendingDecisionsQuery, Result<PagedResult<PendingLendingDecisionDto>>>
{
    public async Task<Result<PagedResult<PendingLendingDecisionDto>>> Handle(ListPendingLendingDecisionsQuery request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var usuario = actor.UserId;
        var persona = usuario is { } u ? await db.Users.AsNoTracking().Where(x => x.Id == u).Select(x => x.PersonId).FirstOrDefaultAsync(ct) : null;
        var hoy = reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var niveles = await db.AttributionLevels.AsNoTracking().ToListAsync(ct);
        var porId = niveles.ToDictionary(n => n.Id);
        var organos = await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var misOrganos = persona is { } p
            ? (await db.GoverningBodyMembers.AsNoTracking().Where(m => m.PersonId == p && m.HasVote && !m.IsDeleted).ToListAsync(ct))
                .Where(m => m.VigenteEn(hoy)).Select(m => m.GoverningBodyId).ToHashSet()
            : [];
        var items = new List<PendingLendingDecisionDto>();

        if (request.Subject is null or LendingDecisionSubjects.CreditApplication)
        {
            var solicitudes = await db.CreditApplications.AsNoTracking().Where(a => !a.IsDeleted && a.RequiredLevelId != null
                    && (a.Status == CreditApplicationStatus.PendingDecision || (a.Status == CreditApplicationStatus.Approved && a.ApprovalValidUntil < hoy)))
                .PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId).ToListAsync(ct);
            var nombres = await ReferenciasDeGobierno.PersonasAsync(db, solicitudes.Select(s => s.ApplicantPersonId), ct);
            var partes = await db.CreditApplicationParties.AsNoTracking().Where(x => solicitudes.Select(s => s.Id).Contains(x.ApplicationId) && !x.IsDeleted)
                .Select(x => new { x.ApplicationId, x.PersonId }).ToListAsync(ct);
            foreach (var s in solicitudes)
            {
                if (!porId.TryGetValue(s.RequiredLevelId!.Value, out var nivel)) continue;
                var resumen = $"Solicitud de {nombres[s.ApplicantPersonId].FullName} por {ErroresDeOriginacion.Pesos(s.RequestedAmount)}";
                var esParte = persona is { } yo && partes.Any(x => x.ApplicationId == s.Id && x.PersonId == yo);
                var item = await ItemAsync(LendingDecisionSubjects.CreditApplication, s.PublicId, s.Number, resumen, s.ConsolidatedExposure ?? s.RequestedAmount,
                    nivel, niveles, s.RoutedAt, SubmitCreditApplicationCommandHandler.Expediente, esParte, usuario, persona, misOrganos, organos, hoy, ct);
                if (item is not null) items.Add(item);
            }
        }

        if (request.Subject is null or LendingDecisionSubjects.CreditLimit)
        {
            var cupos = db.CreditLimits.AsNoTracking().Where(c => !c.IsDeleted && c.DecisionId == null);
            if (!alcance.TodasLasLineas)
            {
                var lineas = alcance.Lineas.ToArray();
                cupos = cupos.Where(c => c.CreditLineId == null || lineas.Contains(c.CreditLineId.Value));
            }
            var candidatas = (await cupos.ToListAsync(ct)).Where(c => !VersionesDeCupo.EsFirme(c)).ToList();
            var todas = await VersionesDeCupo.DeAsync(db, candidatas.Select(c => c.PersonId).ToList(), ct);
            var decididas = await VersionesDeCupo.DecididasAsync(db, todas.Select(c => c.PublicId).ToList(), ct);
            var nombres = await ReferenciasDeGobierno.PersonasAsync(db, candidatas.Select(c => c.PersonId), ct);
            foreach (var c in candidatas)
            {
                if (VersionesDeCupo.Pendiente(todas.Where(v => VersionesDeCupo.MismoCupo(v, c)), decididas)?.PublicId != c.PublicId) continue;
                if (DetalleDeCupo.De(c).Routing is not { } ruta || !porId.TryGetValue(ruta.LevelId, out var nivel)) continue;
                var resumen = $"Cupo de {nombres[c.PersonId].FullName} por {ErroresDeOriginacion.Pesos(c.Amount)}";
                var item = await ItemAsync(LendingDecisionSubjects.CreditLimit, c.PublicId, null, resumen, ruta.Consolidated, nivel,
                    niveles.Where(n => n.MatrixId == nivel.MatrixId).ToList(), c.RegisteredAt, ExpedienteDeCupo.Tipo, persona == c.PersonId, usuario, persona,
                    misOrganos, organos, hoy, ct);
                if (item is not null) items.Add(item);
            }
        }

        var ordenados = items.OrderBy(i => i.Since ?? DateTime.MaxValue).ThenBy(i => i.SubjectPublicId).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        return Result.Success(new PagedResult<PendingLendingDecisionDto>(
            ordenados.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(), pagina.SafePage, pagina.SafePageSize, ordenados.Count));
    }

    private async Task<PendingLendingDecisionDto?> ItemAsync(string sujeto, Guid publicId, string? numero, string resumen, decimal monto, AttributionLevel nivel,
        IReadOnlyList<AttributionLevel> niveles, DateTime? desde, string expediente, bool esParte, int? usuario, int? persona, IReadOnlySet<int> misOrganos,
        IReadOnlyDictionary<int, string> organos, DateOnly hoy, CancellationToken ct)
    {
        var deLaMatriz = niveles.Where(n => n.MatrixId == nivel.MatrixId).ToList();
        string? organo = nivel.GoverningBodyId is { } g ? organos.GetValueOrDefault(g) : null;
        if (nivel.Kind == AttributionLevelKind.Collegiate)
        {
            if (nivel.GoverningBodyId is not { } cuerpo || !misOrganos.Contains(cuerpo)) return null;
            return new PendingLendingDecisionDto(sujeto, publicId, numero, resumen, monto, nivel.Code, nivel.Name, desde, RutasDePendiente.Sesion, organo, false,
                esParte ? RutasDePendiente.ExcluidoPorSerParte : null);
        }
        if (!await atribucion.PuedeDecidirAsync(nivel, deLaMatriz, ct)) return null;

        string? excluido = null;
        if (usuario is not { } u || persona is not { } p) excluido = RutasDePendiente.ExcluidoSinPersona;
        else if (esParte) excluido = RutasDePendiente.ExcluidoPorSerParte;
        else
        {
            var verificada = await intervenciones.VerificarDeAsync(new ExpedienteDeCartera(expediente, publicId), AccionesDeSegregacion.Decidir, hoy, u, p, ct);
            if (verificada.IsFailure)
                excluido = verificada.Error is ErrorConDatos { Data: var datos } && System.Text.Json.JsonSerializer.Serialize(datos).Contains("\"Radicar\"", StringComparison.Ordinal)
                    ? RutasDePendiente.ExcluidoPorRadicar
                    : RutasDePendiente.ExcluidoPorSegregacion;
        }
        return new PendingLendingDecisionDto(sujeto, publicId, numero, resumen, monto, nivel.Code, nivel.Name, desde, RutasDePendiente.Individual, organo,
            excluido is null, excluido);
    }
}
