using FluentValidation;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

// 2026-11-16 (pedido del dueño; feature 024, fase 3): las consultas del proceso de causación, con Lending.AccrualProcesses.Read (traen créditos con su
// número). Todas aplican el alcance de oficina y línea de quien consulta (ProcesosDeCausacion.Visibles / LeerAsync, IAlcanceDeCartera). (nuevas)

/// <summary>Los procesos de causación que ve quien consulta, por estado y por rango (los que se cruzan con él), los más recientes primero.</summary>
public sealed record ListAccrualProcessesQuery(AccrualProcessStatus? Status = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<AccrualProcessListItemDto>>>;

public sealed class ListAccrualProcessesQueryValidator : AbstractValidator<ListAccrualProcessesQuery>
{
    public ListAccrualProcessesQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

public sealed class ListAccrualProcessesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListAccrualProcessesQuery, Result<PagedResult<AccrualProcessListItemDto>>>
{
    public async Task<Result<PagedResult<AccrualProcessListItemDto>>> Handle(ListAccrualProcessesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = ProcesosDeCausacion.Visibles(db.AccrualProcesses.AsNoTracking(), alcance);
        if (request.Status is { } estado) consulta = consulta.Where(p => p.Status == estado);
        if (request.From is { } desde) consulta = consulta.Where(p => p.DateTo >= desde);
        if (request.To is { } hasta) consulta = consulta.Where(p => p.DateFrom <= hasta);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var procesos = await consulta.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);

        var oficinas = procesos.Select(p => p.BranchId).OfType<int>().Distinct().ToList();
        var lineas = procesos.Select(p => p.CreditLineId).OfType<int>().Distinct().ToList();
        var anteriores = procesos.Select(p => p.ReprocessOfId).OfType<int>().Distinct().ToList();
        var nombresDeOficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var codigosDeLinea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        var numeros = await db.AccrualProcesses.AsNoTracking().Where(p => anteriores.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Number, ct);

        var filas = procesos.Select(p => new AccrualProcessListItemDto(p.PublicId, p.Number, p.Kind, p.Status, p.Periodicity, p.SubPeriod, p.DateFrom, p.DateTo,
            ConceptosDeCausacion.Separar(p.Concepts), p.BranchId is { } o ? nombresDeOficina.GetValueOrDefault(o) : null,
            p.CreditLineId is { } l ? codigosDeLinea.GetValueOrDefault(l) : null, p.IncludedAmount, p.LoanCount, p.HasBlockingIssues, p.BatchNumber,
            p.ReprocessOfId is { } r ? numeros.GetValueOrDefault(r) : null, p.CreatedAt)).ToList();
        return Result.Success(new PagedResult<AccrualProcessListItemDto>(filas, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Un proceso de causación con su borrador, su aprobación y su lote. Fuera del alcance o inexistente, <c>Lending.AccrualProcess.NotFound</c>.</summary>
public sealed record GetAccrualProcessQuery(Guid AccrualProcessPublicId) : IRequest<Result<AccrualProcessDto>>;

public sealed class GetAccrualProcessQueryValidator : AbstractValidator<GetAccrualProcessQuery>
{
    public GetAccrualProcessQueryValidator() => RuleFor(x => x.AccrualProcessPublicId).NotEmpty();
}

public sealed class GetAccrualProcessQueryHandler(ProcesosDeCausacion procesos) : IRequestHandler<GetAccrualProcessQuery, Result<AccrualProcessDto>>
{
    public async Task<Result<AccrualProcessDto>> Handle(GetAccrualProcessQuery request, CancellationToken ct)
    {
        var leido = await procesos.LeerAsync(request.AccrualProcessPublicId, seguir: false, ct);
        return leido.IsFailure ? Result.Failure<AccrualProcessDto>(leido.Error) : Result.Success(await procesos.DtoAsync(leido.Value, ct));
    }
}

/// <summary>
/// Las líneas de un proceso —lo de cada crédito y concepto—, por estado, motivo, concepto (tipo de hecho) y número de crédito, sólo de las oficinas y
/// líneas del alcance de quien consulta.
/// </summary>
public sealed record ListAccrualProcessLinesQuery(
    Guid AccrualProcessPublicId,
    AccrualProcessItemStatus? Status = null,
    AccrualExclusionReason? Reason = null,
    string? FactType = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<AccrualProcessLineDto>>>;

public sealed class ListAccrualProcessLinesQueryValidator : AbstractValidator<ListAccrualProcessLinesQuery>
{
    public ListAccrualProcessLinesQueryValidator()
    {
        RuleFor(x => x.AccrualProcessPublicId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Reason).IsInEnum().When(x => x.Reason is not null);
        RuleFor(x => x.FactType).MaximumLength(60);
        RuleFor(x => x.Search).MaximumLength(40);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PageRequest.MaxPageSize);
    }
}

public sealed class ListAccrualProcessLinesQueryHandler(IApplicationDbContext db, ProcesosDeCausacion procesos, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListAccrualProcessLinesQuery, Result<PagedResult<AccrualProcessLineDto>>>
{
    public async Task<Result<PagedResult<AccrualProcessLineDto>>> Handle(ListAccrualProcessLinesQuery request, CancellationToken ct)
    {
        var leido = await procesos.LeerAsync(request.AccrualProcessPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<PagedResult<AccrualProcessLineDto>>(leido.Error);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var id = leido.Value.Id;
        var consulta = db.AccrualProcessLines.AsNoTracking().Where(l => l.AccrualProcessId == id && !l.IsDeleted)
            .PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.Status is { } estado) consulta = consulta.Where(l => l.Status == estado);
        if (request.Reason is { } motivo) consulta = consulta.Where(l => l.ExclusionReason == motivo);
        if (!string.IsNullOrWhiteSpace(request.FactType))
        {
            var tipo = request.FactType.Trim();
            consulta = consulta.Where(l => l.FactType == tipo);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var numero = request.Search.Trim();
            consulta = consulta.Where(l => db.Loans.Any(c => c.Id == l.LoanId && c.Number.Contains(numero)));
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var lineas = await consulta.OrderBy(l => l.Status).ThenBy(l => l.LoanId).ThenBy(l => l.FactType).ThenBy(l => l.Concept)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);

        var creditos = lineas.Select(l => l.LoanId).Distinct().ToList();
        var datos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(c => creditos.Contains(c.Id))
            .Select(c => new { c.Id, c.PublicId, c.Number }).ToDictionaryAsync(c => c.Id, ct);
        var oficinas = lineas.Select(l => l.BranchId).Distinct().ToList();
        var nombres = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var lineasDeCredito = lineas.Select(l => l.CreditLineId).Distinct().ToList();
        var codigos = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(c => lineasDeCredito.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);

        var filas = lineas.Select(l => new AccrualProcessLineDto(datos[l.LoanId].PublicId, datos[l.LoanId].Number, nombres.GetValueOrDefault(l.BranchId),
            codigos.GetValueOrDefault(l.CreditLineId), l.FactType, l.Concept, l.Amount, l.PreviousAmount, l.Status, l.ExclusionReason, l.InstallmentNumber,
            l.RecognitionDate, AccrualCutoverDetailDto.De(l.Detail))).ToList();
        return Result.Success(new PagedResult<AccrualProcessLineDto>(filas, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// Lo que la pantalla necesita para armar un proceso (<c>GET /api/lending/accrual-processes/options</c>): cada concepto con su modo de hoy
/// (<c>Causacion.Modo*</c>) y lo que espera contabilizarse —hechos, valor y los días más viejo y más nuevo— en las oficinas del alcance; las
/// periodicidades y hoy (la fecha local de la cooperativa). Sin datos de deudores.
/// </summary>
public sealed record GetAccrualProcessOptionsQuery : IRequest<Result<AccrualProcessOptionsDto>>;

public sealed class GetAccrualProcessOptionsQueryValidator : AbstractValidator<GetAccrualProcessOptionsQuery>
{
}

public sealed class GetAccrualProcessOptionsQueryHandler(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj,
    Accrual.LectorDePoliticaDeReconocimiento? politica = null) : IRequestHandler<GetAccrualProcessOptionsQuery, Result<AccrualProcessOptionsDto>>
{
    public async Task<Result<AccrualProcessOptionsDto>> Handle(GetAccrualProcessOptionsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Select(b => new { b.Id, b.PublicId }).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var tipos = ConceptosDeCausacion.Todos.Select(c => c.TipoDeHecho).ToList();
        var pendientes = (await db.IntegrationMessageDeliveries.AsNoTracking()
                .Where(d => d.Destination == IntegrationDestinations.Accounting && d.Status == DeliveryStatus.InBatch
                            && d.BatchId == null && d.BatchScopeKey != null && d.BatchScopeKey.StartsWith(ModoDePasoDeCartera.PrefijoDeCausacionManual)
                            && tipos.Contains(d.Message!.Type))
                .Select(d => new { d.BatchScopeKey, d.Message!.Type, d.Message.PublicId, d.Message.BranchPublicId })
                .ToListAsync(ct))
            .Where(x => oficinas.TryGetValue(x.BranchPublicId, out var o) && alcance.IncluyeOficina(o))
            .ToList();
        var mensajes = pendientes.Select(x => x.PublicId).ToList();
        var totales = new Dictionary<Guid, decimal>();
        foreach (var trozo in mensajes.Chunk(500))
            foreach (var parte in await db.AccountingFactParts.AsNoTracking().Where(p => p.MessagePublicId != null && trozo.Contains(p.MessagePublicId.Value))
                         .Select(p => new { Mensaje = p.MessagePublicId!.Value, p.TotalAmount }).ToListAsync(ct))
                totales[parte.Mensaje] = totales.GetValueOrDefault(parte.Mensaje) + parte.TotalAmount;

        var conceptos = new List<AccrualConceptOptionDto>();
        foreach (var c in ConceptosDeCausacion.Todos)
        {
            var modo = await parametros.LeerAsync(ParametrosDeCartera.Modulo, c.ParametroDeModo, hoy, ct: ct);
            var suyos = pendientes.Where(x => x.Type == c.TipoDeHecho).ToList();
            var dias = suyos.Select(x => DateOnly.ParseExact(x.BatchScopeKey![ModoDePasoDeCartera.PrefijoDeCausacionManual.Length..], "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture)).ToList();
            conceptos.Add(new AccrualConceptOptionDto(c.Concepto, c.TipoDeHecho, c.Nombre,
                modo.IsSuccess && modo.Value.Texto == ParametrosDeCartera.CausacionManual, suyos.Count, suyos.Sum(x => totales.GetValueOrDefault(x.PublicId)),
                dias.Count == 0 ? null : dias.Min(), dias.Count == 0 ? null : dias.Max()));
        }
        // Los catálogos de los filtros: las oficinas y las líneas de colocación del alcance, y las pagadurías con convenio (personas jurídicas).
        var ramas = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.PublicId, b.Name }).ToListAsync(ct);
        var lineas = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Nature == LineNature.Placement).OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.PublicId, l.Code, l.Name }).ToListAsync(ct);
        var pagadores = await db.PayrollAgreements.AsNoTracking().Where(a => !a.IsDeleted).Select(a => a.PaymasterPersonId).Distinct().ToListAsync(ct);
        var pagadurias = await ReferenciasDeGobierno.PersonasAsync(db, pagadores, ct);
        // 2026-11-16 (feature 024, fase 4): el reconocimiento al inicio del ciclo, si alguna línea del alcance lo usa.
        var lineasDelAlcance = lineas.Where(l => alcance.IncluyeLinea(l.Id)).Select(l => l.Id).ToList();
        var deReconocimiento = politica is null ? Accrual.PoliticaDeReconocimiento.Diaria : await politica.LeerAsync(hoy, lineasDelAlcance, ct);
        var alInicio = deReconocimiento.LineasAlInicioDelCiclo.Count > 0;
        // 2026-11-24 (feature 025, fase 3): el recálculo de la carga desde el corte, si hay créditos cargados vivos sin recalcular.
        var cargados = await db.Loans.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.Origin == LoanOrigin.Migration && l.Status == LoanStatus.Active
            && db.RepaymentScheduleVersions.Any(v => v.LoanId == l.Id && v.IsMigratedPlan)
            && db.LoanControls.Any(c => c.LoanId == l.Id && c.CutoverAdjustedAt == null), ct);
        List<AccrualProcessKind> clases = alInicio ? [AccrualProcessKind.Posting, AccrualProcessKind.PeriodRecognition] : [AccrualProcessKind.Posting];
        if (cargados) clases.Add(AccrualProcessKind.CutoverRecalculation);
        return Result.Success(new AccrualProcessOptionsDto(conceptos,
            [AccrualProcessPeriodicity.Monthly, AccrualProcessPeriodicity.Biweekly, AccrualProcessPeriodicity.TenDay, AccrualProcessPeriodicity.Weekly,
                AccrualProcessPeriodicity.Daily], hoy,
            ramas.Where(b => alcance.IncluyeOficina(b.Id)).Select(b => new RefDeFiltroDto(b.PublicId, null, b.Name)).ToList(),
            lineas.Where(l => alcance.IncluyeLinea(l.Id)).Select(l => new RefDeFiltroDto(l.PublicId, l.Code, l.Name)).ToList(),
            pagadurias.Values.OrderBy(p => p.FullName, StringComparer.CurrentCulture).Select(p => new RefDeFiltroDto(p.PersonPublicId, null, p.FullName)).ToList(),
            alcance.TodasLasOficinas, alcance.TodasLasLineas, alInicio, deReconocimiento.Modo.ToString(), clases));
    }
}
