using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

// Feature 013, US2 (bloque 6B; T0405; api.md §7.1): las consultas de cupos. Un cupo por línea fuera del alcance de líneas de quien pregunta no
// aparece (la lista filtra en silencio); el consolidado y el rotativo sin línea son de la persona y se ven con el permiso de la ruta. (nuevo)

/// <summary>Un disponible (api.md §7.1 <c>{ limit, used, reserved, approvedNotDisbursed, available }</c>). (nuevo)</summary>
public sealed record CreditLimitAvailabilityItemDto(
    Guid CreditLimitPublicId,
    CreditLimitLineDto? Line,
    decimal Limit,
    decimal Used,
    decimal Reserved,
    decimal ApprovedNotDisbursed,
    decimal Available);

/// <summary>El disponible de una persona a una fecha (api.md §7.1 <c>GET /availability</c>). (nuevo)</summary>
public sealed record CreditLimitAvailabilityDto(
    PersonaRefDto Person,
    DateOnly Date,
    CreditLimitAvailabilityItemDto? Consolidated,
    IReadOnlyList<CreditLimitAvailabilityItemDto> ByLine,
    IReadOnlyList<CreditLimitAvailabilityItemDto> Revolving,
    string Status,
    int? DaysPastDue,
    IReadOnlyList<DebtorBlockDto> Blocks,
    ApplicableAvailabilityDto Applicable);

/// <summary>El disponible que aplica a una línea pedida (nulo sin línea o sin cupo que aplique). (nuevo)</summary>
public sealed record ApplicableAvailabilityDto(decimal? Available, Guid? CreditLimitPublicId);

/// <summary>
/// El disponible de cupo de una persona (api.md §7.1, <c>GET /api/lending/credit-limits/availability?personPublicId=&amp;creditLinePublicId=&amp;date=</c>,
/// <c>Lending.CreditLimits.Read</c>): calculado al leer (FR-058), con los bloqueos vigentes y la mora máxima de sus créditos vivos. Con una línea,
/// también el disponible que aplica a esa línea (el de la línea, el consolidado o el rotativo, C54). (nuevo)
/// </summary>
public sealed record GetCreditLimitAvailabilityQuery(Guid PersonPublicId, Guid? CreditLinePublicId = null, DateOnly? Date = null)
    : IRequest<Result<CreditLimitAvailabilityDto>>;

public sealed class GetCreditLimitAvailabilityQueryValidator : AbstractValidator<GetCreditLimitAvailabilityQuery>
{
    public GetCreditLimitAvailabilityQueryValidator() => RuleFor(x => x.PersonPublicId).NotEmpty();
}

public sealed class GetCreditLimitAvailabilityQueryHandler(
    IApplicationDbContext db,
    DisponibleDeCupo disponibles,
    BloqueosDelDeudor bloqueos,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetCreditLimitAvailabilityQuery, Result<CreditLimitAvailabilityDto>>
{
    public async Task<Result<CreditLimitAvailabilityDto>> Handle(GetCreditLimitAvailabilityQuery request, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<CreditLimitAvailabilityDto>(ErroresDeGobierno.PersonaInexistente());
        var fecha = request.Date ?? reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        int? lineaId = null;
        if (request.CreditLinePublicId is { } lineaPedida)
        {
            var linea = await Configuration.ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, lineaPedida, seguir: false, ct);
            if (linea.IsFailure) return Result.Failure<CreditLimitAvailabilityDto>(linea.Error);
            lineaId = linea.Value.Id;
        }

        var d = await disponibles.DetalleAsync(persona.Id, fecha, ct);
        var lineas = d.PorLinea.Concat(d.Rotativos).Where(c => c.Cupo.CreditLineId is not null).Select(c => c.Cupo.CreditLineId!.Value).Distinct().ToList();
        var lineasDto = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new CreditLimitLineDto(l.PublicId, l.Code, l.Name), ct);
        CreditLimitAvailabilityItemDto Item(DisponibleDeUnCupo c) => new(c.Cupo.PublicId,
            c.Cupo.CreditLineId is { } l ? lineasDto.GetValueOrDefault(l) : null, c.Limite, c.Usado, c.Reservado, c.AprobadoSinDesembolsar, c.Disponible);

        var vigentes = await bloqueos.VigentesAsync([persona.Id], ct);
        var mora = await MoraMaximaAsync(persona.Id, fecha, ct);
        var aplica = lineaId is { } id ? await disponibles.CalcularAsync(persona.Id, id, fecha, ct) : null;
        var hayCupo = d.Consolidado is not null || d.PorLinea.Count > 0 || d.Rotativos.Count > 0;
        var estado = !hayCupo ? EstadosDeCupo.Terminado : vigentes.Count > 0 ? EstadosDeCupo.Bloqueado : EstadosDeCupo.Activo;

        return Result.Success(new CreditLimitAvailabilityDto(
            (await ReferenciasDeGobierno.PersonasAsync(db, [persona.Id], ct))[persona.Id],
            fecha,
            d.Consolidado is null ? null : Item(d.Consolidado),
            d.PorLinea.Where(c => c.Cupo.CreditLineId is { } l && alcance.IncluyeLinea(l)).Select(Item).ToList(),
            d.Rotativos.Where(c => c.Cupo.CreditLineId is not { } l || alcance.IncluyeLinea(l)).Select(Item).ToList(),
            estado,
            mora,
            await ConsultasDeBloqueos.DtosAsync(db, vigentes, ct),
            new ApplicableAvailabilityDto(aplica is { TieneCupo: true } a ? a.Disponible : null, aplica?.CreditLimitPublicId)));
    }

    /// <summary>La mora más alta de los créditos vivos de la persona en su última posición a la fecha (nula sin créditos).</summary>
    private async Task<int?> MoraMaximaAsync(int personId, DateOnly fecha, CancellationToken ct)
    {
        var creditos = await db.Loans.AsNoTracking().Where(l => !l.IsDeleted && l.DebtorPersonId == personId
            && (l.Status == LoanStatus.Active || l.Status == LoanStatus.PendingDisbursement)).Select(l => l.Id).ToListAsync(ct);
        if (creditos.Count == 0) return null;
        var posiciones = await db.LoanDailyPositions.AsNoTracking().Where(p => creditos.Contains(p.LoanId) && p.PositionDate <= fecha)
            .Select(p => new { p.LoanId, p.PositionDate, p.DaysPastDue }).ToListAsync(ct);
        return posiciones.GroupBy(p => p.LoanId).Select(g => g.OrderByDescending(p => p.PositionDate).First().DaysPastDue).DefaultIfEmpty(0).Max();
    }
}

/// <summary>
/// Los cupos (api.md §7.1, <c>GET /api/lending/credit-limits?personPublicId=&amp;kind=&amp;status=&amp;line=&amp;page=</c>,
/// <c>Lending.CreditLimits.Read</c>): por cada cupo (persona, clase, línea) la versión que rige y, si la hay, la que espera decisión, con su estado
/// calculado; filtra por persona, clase, estado y línea. Los cupos por línea fuera del alcance de líneas no aparecen (<c>PorLinea</c>). (nuevo)
/// </summary>
public sealed record ListCreditLimitsQuery(
    Guid? PersonPublicId = null,
    CreditLimitKind? Kind = null,
    string? Status = null,
    Guid? CreditLinePublicId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CreditLimitDto>>>;

public sealed class ListCreditLimitsQueryValidator : AbstractValidator<ListCreditLimitsQuery>
{
    public ListCreditLimitsQueryValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.Status).Must(s => s is null or EstadosDeCupo.PendienteDeDecision or EstadosDeCupo.Activo or EstadosDeCupo.Bloqueado or EstadosDeCupo.Terminado)
            .WithMessage("El estado es PendingDecision, Active, Blocked o Ended.");
    }
}

public sealed class ListCreditLimitsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListCreditLimitsQuery, Result<PagedResult<CreditLimitDto>>>
{
    public async Task<Result<PagedResult<CreditLimitDto>>> Handle(ListCreditLimitsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.CreditLimits.AsNoTracking().Where(c => !c.IsDeleted);
        if (!alcance.TodasLasLineas)
        {
            var lineas = alcance.Lineas.ToArray();
            consulta = consulta.Where(c => c.CreditLineId == null || lineas.Contains(c.CreditLineId.Value));
        }
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(c => db.People.Any(p => p.Id == c.PersonId && p.PublicId == persona));
        if (request.Kind is { } clase) consulta = consulta.Where(c => c.Kind == clase);
        if (request.CreditLinePublicId is { } linea) consulta = consulta.Where(c => db.CreditLines.Any(l => l.Id == c.CreditLineId && l.PublicId == linea));

        var hoy = reloj.HoyLocal;
        var todas = await consulta.ToListAsync(ct);
        var decididas = await VersionesDeCupo.DecididasAsync(db, todas.Select(v => v.PublicId).ToList(), ct);
        var mostradas = new List<CreditLimit>();
        foreach (var grupo in todas.GroupBy(v => (v.PersonId, v.Kind, v.CreditLineId)))
        {
            if ((VersionesDeCupo.Vigente(grupo, hoy) ?? VersionesDeCupo.UltimaFirme(grupo)) is { } rige) mostradas.Add(rige);
            if (VersionesDeCupo.Pendiente(grupo, decididas) is { } pendiente) mostradas.Add(pendiente);
        }

        var dtos = new List<CreditLimitDto>();
        foreach (var v in mostradas.OrderByDescending(v => v.RegisteredAt).ThenByDescending(v => v.Id))
        {
            var dto = await ConsultasDeCupos.DtoAsync(db, v, hoy, ct);
            if (request.Status is null || dto.Status == request.Status) dtos.Add(dto);
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        return Result.Success(new PagedResult<CreditLimitDto>(dtos.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(),
            pagina.SafePage, pagina.SafePageSize, dtos.Count));
    }
}
