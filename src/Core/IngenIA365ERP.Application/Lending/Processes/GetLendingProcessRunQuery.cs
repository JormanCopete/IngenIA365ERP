using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>Un error de una corrida (api.md §16.4): el crédito por su número, el paso, el código, el mensaje, los intentos y el último. (nuevo)</summary>
public sealed record ProcessRunErrorDto(Guid? LoanPublicId, string? LoanNumber, string Step, string Code, string Message, int Attempts, DateTime LastAt,
    bool Resolved);

/// <summary>Una corrida con sus errores paginados, sin datos del deudor. (nuevo)</summary>
public sealed record ProcessRunDto(
    ProcessRunListItemDto Run,
    int? CurrentStep,
    string ExecutionOrigin,
    string? Reason,
    int ProcessedLoans,
    int FailedLoans,
    int SkippedLoans,
    int BatchCount,
    int PartCount,
    int RetryCount,
    string? TotalsJson,
    string? ResultSha256,
    PagedResult<ProcessRunErrorDto> Errors);

/// <summary>
/// <c>GET /api/lending/process-runs/{id}</c> (feature 013, US4, T0499; api.md §16.4, <c>Lending.Processes.View</c>): la corrida y sus errores agrupados
/// por crédito (o de grupo, sin crédito) con los intentos, sin datos del deudor. (nuevo)
/// </summary>
public sealed record GetLendingProcessRunQuery(Guid ProcessRunPublicId, int Page = 1, int PageSize = 50) : IRequest<Result<ProcessRunDto>>;

public sealed class GetLendingProcessRunQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLendingProcessRunQuery, Result<ProcessRunDto>>
{
    public async Task<Result<ProcessRunDto>> Handle(GetLendingProcessRunQuery request, CancellationToken ct)
    {
        var r = await db.LendingProcessRuns.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ProcessRunPublicId && !x.IsDeleted, ct);
        if (r is null) return Result.Failure<ProcessRunDto>(ErroresDeCorridas.Inexistente());

        var filas = await (from e in db.LendingProcessRunErrors.AsNoTracking()
                           join l in db.Loans.AsNoTracking() on e.LoanId equals l.Id into creditos
                           from l in creditos.DefaultIfEmpty()
                           where e.ProcessRunId == r.Id
                           select new { e.LoanId, LoanPublicId = (Guid?)l.PublicId, LoanNumber = l.Number, e.Step, e.ErrorCode, e.ErrorMessage, e.OccurredAt, e.ResolvedAt, e.Id })
            .ToListAsync(ct);
        var agrupados = filas.GroupBy(e => e.LoanId is null ? $"g:{e.Id}" : $"c:{e.LoanId}")
            .Select(g =>
            {
                var ultimo = g.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).First();
                return new ProcessRunErrorDto(ultimo.LoanPublicId, ultimo.LoanNumber, ultimo.Step, ultimo.ErrorCode, ultimo.ErrorMessage, g.Count(),
                    ultimo.OccurredAt, ultimo.ResolvedAt is not null);
            })
            .OrderByDescending(e => e.LastAt).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var errores = new PagedResult<ProcessRunErrorDto>(agrupados.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList(),
            pagina.SafePage, pagina.SafePageSize, agrupados.Count);
        return Result.Success(new ProcessRunDto(ListLendingProcessRunsQueryHandler.Item(r, filas.Count), r.CurrentStep, r.ExecutionOrigin, r.Reason,
            r.ProcessedLoans, r.FailedLoans, r.SkippedLoans, r.BatchCount, r.PartCount, r.RetryCount, r.TotalsJson, r.ResultSha256, errores));
    }
}
