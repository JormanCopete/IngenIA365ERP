using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Una candidata de la calificación, con su causa y en palabras. (nuevo)</summary>
public sealed record RatingCandidateDto(string Category, string Cause, string Detail);

/// <summary>La calificación informativa de un crédito a una fecha (api.md §12.2). (nuevo)</summary>
public sealed record DailyRatingDto(
    Guid LoanPublicId,
    string LoanNumber,
    RefDto Debtor,
    string Modality,
    int DaysPastDue,
    string Category,
    string? Subcategory,
    string Cause,
    string? OfficialCategory,
    string Label);

/// <summary>La calificación oficial del último cierre vigente. (nuevo)</summary>
public sealed record OfficialRatingDto(Guid MonthlyClosePublicId, string Kind, int Year, int Month, int Version, string Category, string? Subcategory,
    string AccountingCategory, string Cause, decimal TotalImpairment);

/// <summary>Lo que se sabe de la calificación de un crédito: la informativa, la oficial y el análisis vigente. (nuevo)</summary>
public sealed record LoanRatingDto(
    Guid LoanPublicId,
    string LoanNumber,
    DateOnly Date,
    DateTime? RecordedAsOf,
    string Label,
    string Category,
    string? Subcategory,
    string Cause,
    string AccountingCategory,
    int DaysPastDue,
    IReadOnlyList<RatingCandidateDto> Candidates,
    IReadOnlyList<string> Explanation,
    OfficialRatingDto? Official,
    AnalysisRatingDto? Analysis);

/// <summary>
/// <c>GET /api/lending/risk/ratings?date=&amp;recordedAsOf=&amp;line=&amp;office=&amp;category=&amp;cause=&amp;page=</c> (feature 013, US9, T0701; api.md
/// §12.2, <c>Lending.Ratings.Read</c>): la calificación <b>informativa</b> de los créditos vivos del alcance a la fecha, con el mismo motor del
/// cierre (no se guarda) y la oficial del último cierre vigente al lado; se rotula «Informativa». Los créditos se paginan por número: sin filtro
/// se califica sólo la página; con filtro de categoría o causa, la calificación calculada de todos los vivos del alcance. (nuevo)
/// </summary>
public sealed record ListDailyRatingsQuery(
    DateOnly? Date = null,
    DateTime? RecordedAsOf = null,
    Guid? Line = null,
    Guid? Office = null,
    RatingCategory? Category = null,
    RatingCause? Cause = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<DailyRatingDto>>>;

public sealed class ListDailyRatingsQueryValidator : AbstractValidator<ListDailyRatingsQuery>
{
    public ListDailyRatingsQueryValidator()
    {
        RuleFor(x => x.Category).IsInEnum().When(x => x.Category is not null);
        RuleFor(x => x.Cause).IsInEnum().When(x => x.Cause is not null);
    }
}

public sealed class ListDailyRatingsQueryHandler(
    IApplicationDbContext db,
    CalificacionInformativa informativa,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj) : IRequestHandler<ListDailyRatingsQuery, Result<PagedResult<DailyRatingDto>>>
{
    public async Task<Result<PagedResult<DailyRatingDto>>> Handle(ListDailyRatingsQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = CargadorDeRiesgo.VivosAl(db.Loans.AsNoTracking(), fecha).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.Line is { } linea) consulta = consulta.Where(l => db.CreditLines.Any(c => c.Id == l.CreditLineId && c.PublicId == linea));
        if (request.Office is { } oficina) consulta = consulta.Where(l => db.Branches.Any(b => b.Id == l.BranchId && b.PublicId == oficina));
        var pagina = new PageRequest(request.Page, request.PageSize);
        var ordenada = consulta.OrderBy(l => l.Number).ThenBy(l => l.Id);
        // Sin filtro de categoría ni de causa se califica sólo la página (con los créditos de sus deudores para el arrastre); con filtro, todos.
        var sinFiltro = request.Category is null && request.Cause is null;
        var total = sinFiltro ? await ordenada.CountAsync(ct) : 0;
        var creditos = sinFiltro
            ? await ordenada.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct)
            : await ordenada.ToListAsync(ct);

        var calculo = await informativa.CalcularAsync(creditos.Select(c => c.Id).ToList(), fecha, request.RecordedAsOf, sinAnalisis: false, ct);
        if (calculo.IsFailure) return Result.Failure<PagedResult<DailyRatingDto>>(calculo.Error);
        var porCredito = calculo.Value.Calculados.ToDictionary(c => c.Credito.Credito.Id);
        var oficiales = await new CategoriaContableDeCartera(db).AlCorteAsync(creditos.Select(c => c.Id).ToList(), fecha, ct);

        var filtrados = creditos.Where(c => porCredito.ContainsKey(c.Id)).Select(c => (Credito: c, Riesgo: porCredito[c.Id]))
            .Where(x => request.Category is not { } cat || x.Riesgo.Calificacion.Categoria == cat)
            .Where(x => request.Cause is not { } causa || x.Riesgo.Calificacion.Causa == causa)
            .ToList();
        var vista = sinFiltro ? filtrados : filtrados.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var (personas, _, _) = await ListLoansQueryHandler.ReferenciasAsync(db, vista.Select(v => v.Credito).ToList(), ct);
        var items = vista.Select(v =>
        {
            var cal = v.Riesgo.Calificacion;
            var oficial = oficiales[v.Credito.Id];
            return new DailyRatingDto(v.Credito.PublicId, v.Credito.Number, personas[v.Credito.DebtorPersonId], v.Riesgo.Credito.Vigentes.ModalityCode,
                v.Riesgo.Credito.DiasDeMora, cal.Categoria.ToString(), cal.Subcategoria == RatingSubcategory.None ? null : cal.Subcategoria.ToString(),
                cal.Causa.ToString(), oficial.MonthlyClosePublicId is null ? null : oficial.Category, CalificacionInformativa.Rotulo);
        }).ToList();
        return Result.Success(new PagedResult<DailyRatingDto>(items, pagina.SafePage, pagina.SafePageSize, sinFiltro ? total : filtrados.Count));
    }
}

/// <summary>
/// <c>GET /api/lending/loans/{id}/rating?date=&amp;recordedAsOf=</c> (feature 013, US9, T0701; <c>Lending.Ratings.Read</c>): la calificación
/// informativa del crédito a la fecha (con R, lo que se sabía entonces) con sus candidatas y su explicación, la oficial del último cierre vigente y
/// el análisis vigente. No escribe nada. El crédito fuera del alcance es el 404 del crédito. (nuevo)
/// </summary>
public sealed record GetLoanRatingQuery(Guid LoanPublicId, DateOnly? Date = null, DateTime? RecordedAsOf = null) : IRequest<Result<LoanRatingDto>>;

public sealed class GetLoanRatingQueryHandler(
    IApplicationDbContext db,
    CalificacionInformativa informativa,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetLoanRatingQuery, Result<LoanRatingDto>>
{
    public async Task<Result<LoanRatingDto>> Handle(GetLoanRatingQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<LoanRatingDto>(leido.Error);
        var credito = leido.Value;
        var fecha = request.Date ?? reloj.HoyLocal;

        var calculo = await informativa.CalcularAsync([credito.Id], fecha, request.RecordedAsOf, sinAnalisis: false, ct);
        if (calculo.IsFailure) return Result.Failure<LoanRatingDto>(calculo.Error);
        if (calculo.Value.Errores.TryGetValue(credito.Id, out var error)) return Result.Failure<LoanRatingDto>(error);
        var r = calculo.Value.Calculados.Single();
        var cal = r.Calificacion;

        OfficialRatingDto? oficial = null;
        var cierre = await CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, fecha.AddDays(1), ct);
        if (cierre is not null && await CategoriaContableDeCartera.LineasDe(db, cierre).FirstOrDefaultAsync(l => l.LoanId == credito.Id, ct) is { } linea)
            oficial = new OfficialRatingDto(cierre.PublicId, cierre.Kind.ToString(), cierre.PeriodYear, cierre.PeriodMonth, cierre.Version,
                linea.FinalRating.ToString(), linea.FinalSubcategory == RatingSubcategory.None ? null : linea.FinalSubcategory.ToString(),
                linea.AccountingRating.ToString(), linea.RatingCause.ToString(), linea.TotalImpairment);

        var analisis = await db.AnalysisRatings.AsNoTracking().Where(a => a.LoanId == credito.Id && a.LiftedAt == null && !a.IsDeleted)
            .FirstOrDefaultAsync(ct);
        return Result.Success(new LoanRatingDto(credito.PublicId, credito.Number, fecha, request.RecordedAsOf, CalificacionInformativa.Rotulo,
            cal.Categoria.ToString(), cal.Subcategoria == RatingSubcategory.None ? null : cal.Subcategoria.ToString(), cal.Causa.ToString(),
            cal.Contable.ToString(), r.Credito.DiasDeMora,
            cal.Candidatas.Select(c => new RatingCandidateDto(c.Categoria.ToString(), c.Causa.ToString(), c.Detalle)).ToList(),
            cal.Explicacion.Lineas, oficial, analisis is null ? null : ConsultasDeAnalisis.Dto(analisis, credito.PublicId, credito.Number)));
    }
}
