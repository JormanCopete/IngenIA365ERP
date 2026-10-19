using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;

/// <summary>Lo común de las solicitudes de tasa negociada: su expediente de segregación y su forma de respuesta. (nuevo)</summary>
public static class SolicitudesDeTasaNegociada
{
    /// <summary>El expediente de la segregación (US16, T163): pedir es Radicar y decidir es Decidir.</summary>
    public const string Expediente = SegregacionDeCaptaciones.TasaNegociada;

    public static ExpedienteDeCartera ExpedienteDe(Guid solicitud) => new(Expediente, SegregacionDeCaptaciones.Raiz(Expediente, solicitud));

    public static async Task<IReadOnlyList<NegotiatedRateRequestDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<NegotiatedRateRequest> solicitudes,
        CancellationToken ct)
    {
        var cuentasIds = solicitudes.Select(s => s.MemberAccountId).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => cuentasIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var personas = await LectorDeCuentas.PersonasAsync(db, cuentas.Values.Select(a => a.HolderPersonId), ct);
        var reglasIds = solicitudes.Select(s => s.NegotiatedRateRuleId).Distinct().ToList();
        var reglas = await db.NegotiatedRateRules.AsNoTracking().Where(r => reglasIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code, ct);
        var usuariosIds = solicitudes.Select(s => s.RequestedByUserId).Concat(solicitudes.Where(s => s.DecidedByUserId is not null).Select(s => s.DecidedByUserId!.Value))
            .Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuariosIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        return solicitudes.Select(s =>
        {
            var cuenta = cuentas[s.MemberAccountId];
            return new NegotiatedRateRequestDto(s.PublicId, cuenta.PublicId, cuenta.Number, personas[cuenta.HolderPersonId], s.Amount, s.TermDays, s.TableAnnualRate,
                s.RequestedSpread, s.MaxSpread, reglas.GetValueOrDefault(s.NegotiatedRateRuleId) ?? string.Empty, usuarios.GetValueOrDefault(s.RequestedByUserId) ?? string.Empty,
                s.Status, null, null, s.Reason, s.RequestedAt, s.DecidedByUserId is { } d ? usuarios.GetValueOrDefault(d) : null, s.DecidedAt, s.DecisionReason);
        }).ToList();
    }

    /// <summary>La solicitud viva del CDAT en alcance (404 de la solicitud fuera de él).</summary>
    public static async Task<Result<(NegotiatedRateRequest Solicitud, MemberAccount Cuenta)>> EnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var solicitud = await db.NegotiatedRateRequests.FirstOrDefaultAsync(r => r.PublicId == publicId && !r.IsDeleted, ct);
        if (solicitud is null) return Result.Failure<(NegotiatedRateRequest, MemberAccount)>(ErroresDeCdat.SolicitudInexistente());
        var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == solicitud.MemberAccountId, ct);
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, ErroresDeCdat.SolicitudInexistente(), auditoria, nameof(NegotiatedRateRequest),
            solicitud.PublicId, ct);
        return dentro.IsFailure
            ? Result.Failure<(NegotiatedRateRequest, MemberAccount)>(dentro.Error)
            : Result.Success((solicitud, cuenta));
    }
}

/// <summary>Las solicitudes de tasa negociada (api.md §6, <c>GET /api/lending/negotiated-rate-requests</c>), las del alcance, las más recientes primero. (nuevo)</summary>
public sealed record ListNegotiatedRateRequestsQuery(NegotiatedRateRequestStatus? Status = null, Guid? LinePublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<NegotiatedRateRequestDto>>>;

public sealed class ListNegotiatedRateRequestsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListNegotiatedRateRequestsQuery, Result<PagedResult<NegotiatedRateRequestDto>>>
{
    public async Task<Result<PagedResult<NegotiatedRateRequestDto>>> Handle(ListNegotiatedRateRequestsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted).PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.LinePublicId is { } linea) cuentas = cuentas.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.PublicId == linea));
        var consulta = db.NegotiatedRateRequests.AsNoTracking().Where(r => !r.IsDeleted && cuentas.Any(a => a.Id == r.MemberAccountId));
        if (request.Status is { } estado) consulta = consulta.Where(r => r.Status == estado);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id).Skip((pagina.SafePage - 1) * pagina.SafePageSize)
            .Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<NegotiatedRateRequestDto>(await SolicitudesDeTasaNegociada.DtosAsync(db, filas, ct), pagina.SafePage,
            pagina.SafePageSize, total));
    }
}

/// <summary>Una solicitud de tasa negociada (api.md §6, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetNegotiatedRateRequestQuery(Guid PublicId) : IRequest<Result<NegotiatedRateRequestDto>>;

public sealed class GetNegotiatedRateRequestQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetNegotiatedRateRequestQuery, Result<NegotiatedRateRequestDto>>
{
    public async Task<Result<NegotiatedRateRequestDto>> Handle(GetNegotiatedRateRequestQuery request, CancellationToken ct)
    {
        var leida = await SolicitudesDeTasaNegociada.EnAlcanceAsync(db, alcance, auditoria, request.PublicId, ct);
        return leida.IsFailure
            ? Result.Failure<NegotiatedRateRequestDto>(leida.Error)
            : Result.Success((await SolicitudesDeTasaNegociada.DtosAsync(db, [leida.Value.Solicitud], ct))[0]);
    }
}

/// <summary>Aprobar o negar una tasa negociada (api.md §6). (nuevo)</summary>
public enum NegotiatedRateDecision
{
    Approve = 1,
    Reject = 2,
}

/// <summary>
/// Decide una tasa negociada (feature 014, US6, T171; api.md §6 <c>approve</c>/<c>reject</c>, <c>Lending.NegotiatedRates.Approve</c>; FR-127): la solicitud en
/// espera (<c>Lending.NegotiatedRate.AlreadyDecided</c>), del CDAT en apertura, quien decide no es quien la pidió (segregación por persona,
/// <c>Lending.Segregation.Conflict</c>) y tiene el nivel que la matriz pide para el margen y el monto (<see cref="AtribucionDelDecisor.ExigirAsync"/>; un nivel
/// colegiado se decide con el punto de acta cerrado de la solicitud, <c>sessionItemPublicId</c>); sin matriz, basta el permiso. Al aprobar, el margen no puede
/// pasar el máximo de la regla vigente (<c>MarginAboveRule</c>) y la propuesta del CDAT sella el margen, la regla, el aprobador y el motivo; al negar, la propuesta
/// sigue con la tasa de la tabla. Aprobada o negada, el CDAT queda esperando el fondeo. (nuevo)
/// </summary>
public sealed record DecideNegotiatedRateCommand(Guid RequestPublicId, NegotiatedRateDecision Decision, string Reason, Guid? ApprovalPublicId = null,
    Guid? SessionItemPublicId = null) : IRequest<Result<NegotiatedRateRequestDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DecideNegotiatedRateCommandValidator : ValidadorConMotivo<DecideNegotiatedRateCommand>
{
    public DecideNegotiatedRateCommandValidator()
    {
        RuleFor(x => x.RequestPublicId).NotEmpty();
        RuleFor(x => x.Decision).IsInEnum();
    }
}

public sealed class DecideNegotiatedRateCommandHandler(
    IApplicationDbContext db,
    NegociacionDeTasa negociacion,
    AtribucionDelDecisor atribucion,
    PuntoDeActaCerrado actas,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<DecideNegotiatedRateCommand, Result<NegotiatedRateRequestDto>>
{
    public async Task<Result<NegotiatedRateRequestDto>> Handle(DecideNegotiatedRateCommand request, CancellationToken ct)
    {
        var leida = await SolicitudesDeTasaNegociada.EnAlcanceAsync(db, alcance, auditoria, request.RequestPublicId, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (solicitud, cuenta) = leida.Value;
        if (!solicitud.EnEspera) return Falla(ErroresDeCdat.YaDecidida(solicitud.Status.ToString()));
        var hoy = reloj.HoyLocal;

        // El nivel que pide la matriz (o el permiso, sin matriz).
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstAsync(l => l.Id == cuenta.CreditLineId, ct);
        var nivel = await negociacion.NivelAsync(linea.Code, solicitud.Amount, solicitud.RequestedSpread, hoy, ct);
        if (nivel.IsFailure) return Falla(nivel.Error);
        if (nivel.Value.Nivel is { } efectivo)
        {
            if (efectivo.Kind == AttributionLevelKind.Collegiate)
            {
                if (request.SessionItemPublicId is not { } punto)
                    return Falla(await atribucion.ExigirAsync(efectivo, nivel.Value.Niveles, ErroresDeCdat.SolicitudInexistente(), ct) ?? ErroresDeCdat.SolicitudInexistente());
                var cerrado = await actas.ValidarAsync(punto, LendingDecisionSubjects.NegotiatedRate, solicitud.PublicId, ct);
                if (cerrado.IsFailure) return Falla(cerrado.Error);
            }
            else if (await atribucion.ExigirAsync(efectivo, nivel.Value.Niveles, ErroresDeCdat.SolicitudInexistente(), ct) is { } sinNivel)
            {
                return Falla(sinNivel);
            }
        }
        else if (!(await atribucion.ConcedidosAsync(ct)).Contains(NegociacionDeTasa.PermisoDeAprobacion))
        {
            return Falla(ErroresDeCdat.SolicitudInexistente());
        }

        var verificada = await intervenciones.VerificarAsync(SolicitudesDeTasaNegociada.ExpedienteDe(solicitud.PublicId), AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var propuesta = PropuestaDeCdat.Leer(cuenta.PendingTermsJson);
        if (cuenta.Status != MemberAccountStatus.Opening || propuesta is null)
            return Falla(ErroresDeCdat.YaFondeado(cuenta.Number));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            if (request.Decision == NegotiatedRateDecision.Approve)
            {
                var vigentes = await negociacion.ReglasVigentesAsync(cuenta.CreditLineId, hoy, null, ct);
                var regla = vigentes.FirstOrDefault(r => r.Id == solicitud.NegotiatedRateRuleId)
                            ?? vigentes.FirstOrDefault(r => r.Code == db.NegotiatedRateRules.AsNoTracking().Where(x => x.Id == solicitud.NegotiatedRateRuleId).Select(x => x.Code).FirstOrDefault());
                if (regla is null || solicitud.RequestedSpread > regla.MaxSpread)
                    return Falla(new ErrorConDatos(Domain.Lending.Deposits.TasaNegociada.CodigoMargenSobreLaRegla,
                        $"La regla vigente ya no admite {solicitud.RequestedSpread:P4} sobre la tabla.", new { maxMarginEa = regla?.MaxSpread }));
                solicitud.Aprobar(verificada.Value.UsuarioId, ahora, request.Reason, request.ApprovalPublicId, request.SessionItemPublicId);
                cuenta.PendingTermsJson = (propuesta with
                {
                    Negociacion = new NegociacionDeCdat(solicitud.RequestedSpread, regla.Id, regla.PublicId, verificada.Value.UsuarioId, request.Reason, solicitud.Id),
                }).Serializar();
            }
            else
            {
                solicitud.Negar(verificada.Value.UsuarioId, ahora, request.Reason, request.SessionItemPublicId);
                cuenta.PendingTermsJson = (propuesta with { Negociacion = null }).Serializar();
            }
            intervenciones.Registrar(verificada.Value, SolicitudesDeTasaNegociada.Expediente, solicitud.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success((await SolicitudesDeTasaNegociada.DtosAsync(db, [solicitud], ct))[0]);
        }, ct);
    }

    private static Result<NegotiatedRateRequestDto> Falla(Error e) => Result.Failure<NegotiatedRateRequestDto>(e);
}

/// <summary>
/// Retira una solicitud en espera (api.md §6, <c>POST /{id}/withdraw</c>, <c>Lending.NegotiatedRates.Request</c>): sólo quien la pidió; la propuesta del CDAT
/// sigue con la tasa de la tabla. (nuevo)
/// </summary>
public sealed record WithdrawNegotiatedRateRequestCommand(Guid RequestPublicId, string Reason) : IRequest<Result<NegotiatedRateRequestDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class WithdrawNegotiatedRateRequestCommandValidator : ValidadorConMotivo<WithdrawNegotiatedRateRequestCommand>
{
    public WithdrawNegotiatedRateRequestCommandValidator() => RuleFor(x => x.RequestPublicId).NotEmpty();
}

public sealed class WithdrawNegotiatedRateRequestCommandHandler(IApplicationDbContext db, IActorActual actorActual, IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria, IDateTimeService reloj) : IRequestHandler<WithdrawNegotiatedRateRequestCommand, Result<NegotiatedRateRequestDto>>
{
    public async Task<Result<NegotiatedRateRequestDto>> Handle(WithdrawNegotiatedRateRequestCommand request, CancellationToken ct)
    {
        var leida = await SolicitudesDeTasaNegociada.EnAlcanceAsync(db, alcance, auditoria, request.RequestPublicId, ct);
        if (leida.IsFailure) return Result.Failure<NegotiatedRateRequestDto>(leida.Error);
        var (solicitud, cuenta) = leida.Value;
        if (!solicitud.EnEspera) return Result.Failure<NegotiatedRateRequestDto>(ErroresDeCdat.YaDecidida(solicitud.Status.ToString()));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId != solicitud.RequestedByUserId)
            return Result.Failure<NegotiatedRateRequestDto>(new Error(ErroresDeCdat.CodigoSoloQuienPidio, "Sólo quien pidió la tasa negociada la retira."));
        solicitud.Retirar(actor.UserId!.Value, reloj.UtcNow, request.Reason);
        if (PropuestaDeCdat.Leer(cuenta.PendingTermsJson) is { } propuesta) cuenta.PendingTermsJson = (propuesta with { Negociacion = null }).Serializar();
        await db.SaveChangesAsync(ct);
        return Result.Success((await SolicitudesDeTasaNegociada.DtosAsync(db, [solicitud], ct))[0]);
    }
}

/// <summary>
/// El asunto de acta de la tasa negociada (feature 014, US6, T171): para que un órgano la decida en sesión cuando la matriz la enruta a un nivel colegiado; el
/// cierre no aplica nada (la decisión la registra <c>approve</c> con el punto cerrado), quien la pidió no vota. (nuevo)
/// </summary>
public sealed class AsuntoDeTasaNegociada(IApplicationDbContext db) : IAsuntoDeActa
{
    public string SubjectType => LendingDecisionSubjects.NegotiatedRate;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var s = await db.NegotiatedRateRequests.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == subjectPublicId && !r.IsDeleted, ct);
        if (s is null) return Result.Failure<AsuntoDeActa>(ErroresDeCdat.SolicitudInexistente());
        var numero = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == s.MemberAccountId).Select(a => a.Number).FirstAsync(ct);
        var huella = new HuellaDeCartera().Identificador("request", s.PublicId).Decimal("spread", s.RequestedSpread, 6).Decimal("table", s.TableAnnualRate, 6)
            .Decimal("amount", s.Amount, 2).Calcular();
        var personas = new List<PersonaDelAsunto>();
        if (await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == s.RequestedByUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct) is { } quien)
            personas.Add(new PersonaDelAsunto(quien, MotivosDeExclusion.Radicador));
        return Result.Success(new AsuntoDeActa($"Tasa negociada del CDAT {numero}: {s.RequestedSpread:P4} sobre la tabla", s.Reason, s.EnEspera, s.Status.ToString(),
            SolicitudesDeTasaNegociada.ExpedienteDe(s.PublicId), huella) { Personas = personas, MontoConsolidado = s.Amount });
    }

    public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct) => Task.FromResult(Result.Success());
}
