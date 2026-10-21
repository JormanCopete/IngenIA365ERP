using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk.Evaluations;

/// <summary>
/// Las evaluaciones de cartera (feature 013, K3, US14, T0912; api.md §12.6, <c>GET /api/lending/risk/evaluations?status=</c>,
/// <c>Lending.Ratings.Read</c>): las que tienen algún crédito en las oficinas y líneas del alcance (una evaluación sin créditos del alcance no aparece), las
/// más recientes primero, sin sus créditos. (nuevo)
/// </summary>
public sealed record ListPortfolioEvaluationsQuery(PortfolioEvaluationStatus? Status = null) : IRequest<Result<IReadOnlyList<PortfolioEvaluationDto>>>;

public sealed class ListPortfolioEvaluationsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListPortfolioEvaluationsQuery, Result<IReadOnlyList<PortfolioEvaluationDto>>>
{
    public async Task<Result<IReadOnlyList<PortfolioEvaluationDto>>> Handle(ListPortfolioEvaluationsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var dentro = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.PortfolioEvaluations.AsNoTracking()
            .Where(e => !e.IsDeleted && db.PortfolioEvaluationLoans.Any(l => l.PortfolioEvaluationId == e.Id && !l.IsDeleted && dentro.Contains(l.LoanId)));
        if (request.Status is { } estado) consulta = consulta.Where(e => e.Status == estado);
        var evaluaciones = await consulta.OrderByDescending(e => e.CutoffDate).ThenByDescending(e => e.Id).ToListAsync(ct);
        var salida = new List<PortfolioEvaluationDto>();
        foreach (var e in evaluaciones) salida.Add(await EvaluacionesDeCartera.DtoAsync(db, e, [], 1, 0, ct));
        return Result.Success<IReadOnlyList<PortfolioEvaluationDto>>(salida);
    }
}

/// <summary>
/// Una evaluación de cartera con la página pedida de sus créditos del alcance (feature 013, K3, US14, T0912; api.md §12.6,
/// <c>GET /api/lending/risk/evaluations/{id}?page=</c>, <c>Lending.Ratings.Read</c>): cada crédito con la calificación por las reglas al corte, la propuesta
/// y la justificación. Los créditos fuera del alcance no aparecen; sin ninguno del alcance, <c>Lending.PortfolioEvaluation.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetPortfolioEvaluationQuery(Guid PortfolioEvaluationPublicId, int Page = 1, int PageSize = 50) : IRequest<Result<PortfolioEvaluationDto>>;

public sealed class GetPortfolioEvaluationQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<GetPortfolioEvaluationQuery, Result<PortfolioEvaluationDto>>
{
    public async Task<Result<PortfolioEvaluationDto>> Handle(GetPortfolioEvaluationQuery request, CancellationToken ct)
    {
        var leida = await EvaluacionesDeCartera.LeerAsync(db, request.PortfolioEvaluationPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<PortfolioEvaluationDto>(leida.Error);
        var e = leida.Value;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var dentro = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        var lineas = from l in db.PortfolioEvaluationLoans.AsNoTracking()
                     join c in dentro on l.LoanId equals c.Id
                     where l.PortfolioEvaluationId == e.Id && !l.IsDeleted
                     orderby c.Number
                     select l;
        if (!await lineas.AnyAsync(ct)) return Result.Failure<PortfolioEvaluationDto>(EvaluacionesDeCartera.Inexistente());
        var pagina = new PageRequest(request.Page, request.PageSize);
        var deLaPagina = await lineas.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(await EvaluacionesDeCartera.DtoAsync(db, e, deLaPagina, pagina.SafePage, pagina.SafePageSize, ct));
    }
}
