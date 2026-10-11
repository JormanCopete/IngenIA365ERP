using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (T0396; api.md §4.2): las consultas de solicitudes. Filtran en silencio por la oficina y la línea del AlcanceDeCartera de quien
// pregunta (PorOficinaYLinea); el detalle y el enrutamiento de una solicitud fuera de él responden el mismo 404 que una inexistente (T62). (nuevo)

/// <summary>
/// Las solicitudes (api.md §4.2, <c>GET /api/lending/applications</c>, <c>Lending.Applications.Read</c>): filtros por estado, línea, oficina,
/// solicitante, nivel, las que radicó quien pregunta y rango de radicación; la más reciente primero. (nuevo)
/// </summary>
public sealed record ListCreditApplicationsQuery(
    CreditApplicationStatus? Status = null,
    Guid? CreditLinePublicId = null,
    Guid? OfficePublicId = null,
    Guid? ApplicantPersonPublicId = null,
    string? LevelCode = null,
    bool Mine = false,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CreditApplicationListItemDto>>>;

public sealed class ListCreditApplicationsQueryValidator : AbstractValidator<ListCreditApplicationsQuery>
{
    public ListCreditApplicationsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.LevelCode).MaximumLength(10);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListCreditApplicationsQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ListCreditApplicationsQuery, Result<PagedResult<CreditApplicationListItemDto>>>
{
    public async Task<Result<PagedResult<CreditApplicationListItemDto>>> Handle(ListCreditApplicationsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.CreditApplications.AsNoTracking().Where(a => !a.IsDeleted).PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.Status is { } estado) consulta = consulta.Where(a => a.Status == estado);
        if (request.CreditLinePublicId is { } linea)
            consulta = consulta.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.PublicId == linea));
        if (request.OfficePublicId is { } oficina) consulta = consulta.Where(a => db.Branches.Any(b => b.Id == a.BranchId && b.PublicId == oficina));
        if (request.ApplicantPersonPublicId is { } persona)
            consulta = consulta.Where(a => db.People.Any(p => p.Id == a.ApplicantPersonId && p.PublicId == persona));
        if (!string.IsNullOrWhiteSpace(request.LevelCode))
        {
            var nivel = request.LevelCode.Trim().ToUpperInvariant();
            consulta = consulta.Where(a => db.AttributionLevels.Any(n => n.Id == a.RequiredLevelId && n.Code == nivel));
        }
        if (request.Mine)
        {
            var actor = await actorActual.ObtenerAsync(ct);
            var usuario = actor.UserId ?? -1;
            consulta = consulta.Where(a => a.CreatedByUserId == usuario || a.SubmittedByUserId == usuario);
        }
        if (request.From is { } desde)
        {
            var desdeUtc = desde.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(a => (a.SubmittedAt ?? a.CreatedAt) >= desdeUtc);
        }
        if (request.To is { } hasta)
        {
            var hastaUtc = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(a => (a.SubmittedAt ?? a.CreatedAt) < hastaUtc);
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(a => a.SubmittedAt ?? a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);

        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(a => a.ApplicantPersonId), ct);
        var idsDeLinea = filas.Select(a => a.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var idsDeVersion = filas.Select(a => a.CreditLineVersionId).Distinct().ToList();
        var versiones = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().Where(v => idsDeVersion.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.PublicId, ct);
        var idsDeOficina = filas.Select(a => a.BranchId).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => idsDeOficina.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new CreditApplicationOfficeDto(b.PublicId, b.Name), ct);
        var idsDeNivel = filas.Where(a => a.RequiredLevelId is not null).Select(a => a.RequiredLevelId!.Value).Distinct().ToList();
        var niveles = await db.AttributionLevels.AsNoTracking().Where(n => idsDeNivel.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.Code, ct);
        var hoy = reloj.HoyLocal;

        var items = filas.Select(a => new CreditApplicationListItemDto(a.PublicId, a.Number, personas[a.ApplicantPersonId],
            new CreditApplicationLineDto(lineas[a.CreditLineId].PublicId, lineas[a.CreditLineId].Code, lineas[a.CreditLineId].Name,
                versiones.TryGetValue(a.CreditLineVersionId, out var v) ? v : null),
            oficinas.GetValueOrDefault(a.BranchId), a.RequestedAmount, a.Installments, a.Status,
            a.RequiredLevelId is { } n ? niveles.GetValueOrDefault(n) : null, a.SubmittedAt, a.ApprovalValidUntil, a.AprobacionVencida(hoy))).ToList();
        return Result.Success(new PagedResult<CreditApplicationListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Una solicitud con todo lo suyo (api.md §4.2, <c>GET /{id}</c>; <c>Lending.Application.NotFound</c>, también fuera del alcance). (nuevo)</summary>
public sealed record GetCreditApplicationQuery(Guid ApplicationPublicId) : IRequest<Result<CreditApplicationDto>>;

public sealed class GetCreditApplicationQueryValidator : AbstractValidator<GetCreditApplicationQuery>
{
    public GetCreditApplicationQueryValidator() => RuleFor(x => x.ApplicationPublicId).NotEmpty();
}

public sealed class GetCreditApplicationQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetCreditApplicationQuery, Result<CreditApplicationDto>>
{
    public async Task<Result<CreditApplicationDto>> Handle(GetCreditApplicationQuery request, CancellationToken ct)
    {
        var s = await SolicitudEnAlcance.LeerAsync(db, alcanceDeLaPeticion, auditoria, request.ApplicationPublicId, seguir: false, ct);
        return s.IsFailure
            ? Result.Failure<CreditApplicationDto>(s.Error)
            : Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s.Value, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// El enrutamiento vigente de una solicitud (api.md §4.2, <c>GET /{id}/routing</c>, <c>Lending.Applications.Read</c>): la matriz, el nivel
/// efectivo, las razones, el monto consolidado de hoy, los no exceptuables y la huella que el decisor manda como <c>expectedContentSha256</c>. Se
/// calcula con lo guardado (las validaciones vigentes y el último puntaje) y la exposición de hoy; no escribe. Si difiere de la sellada, decidir
/// reenruta (<c>Lending.Decision.ContentChanged</c>). (nuevo)
/// </summary>
public sealed record GetCreditApplicationRoutingQuery(Guid ApplicationPublicId) : IRequest<Result<CreditApplicationRoutingDto>>;

public sealed class GetCreditApplicationRoutingQueryValidator : AbstractValidator<GetCreditApplicationRoutingQuery>
{
    public GetCreditApplicationRoutingQueryValidator() => RuleFor(x => x.ApplicationPublicId).NotEmpty();
}

public sealed class GetCreditApplicationRoutingQueryHandler(
    IApplicationDbContext db,
    EnrutamientoDeSolicitud enrutamiento,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetCreditApplicationRoutingQuery, Result<CreditApplicationRoutingDto>>
{
    public async Task<Result<CreditApplicationRoutingDto>> Handle(GetCreditApplicationRoutingQuery request, CancellationToken ct)
    {
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcanceDeLaPeticion, auditoria, request.ApplicationPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<CreditApplicationRoutingDto>(leida.Error);
        var s = leida.Value;
        if (s.Status is CreditApplicationStatus.Draft or CreditApplicationStatus.Returned)
            return Result.Failure<CreditApplicationRoutingDto>(ErroresDeOriginacion.NoEstaEnAnalisis(s.Status.ToString()));

        var hoy = reloj.HoyLocal;
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstAsync(l => l.Id == s.CreditLineId, ct);
        var version = await db.CreditLineVersions.AsNoTracking().IgnoreQueryFilters().FirstAsync(v => v.Id == s.CreditLineVersionId, ct);
        var datos = await enrutamiento.DatosVigentesAsync(s, hoy, ct);
        var e = await enrutamiento.CalcularAsync(s, linea, version, datos, hoy, ct);
        if (e.IsFailure) return Result.Failure<CreditApplicationRoutingDto>(e.Error);
        var r = e.Value;
        return Result.Success(new CreditApplicationRoutingDto(r.Matriz.Matriz.VersionNumber, r.Nivel.Code, r.Nivel.Name, r.Nivel.Kind,
            r.Enrutamiento.Razones.Select(x => new RoutingReasonDto(x.Clase, x.Nivel.Codigo, x.Detalle)).ToList(),
            r.Datos.MontoConsolidado, r.Datos.HayVinculado, r.Enrutamiento.NoExceptuables, r.Huella));
    }
}
