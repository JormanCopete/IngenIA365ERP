using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>El avance de una corrida: créditos recorridos y el total. (nuevo)</summary>
public sealed record ProcessRunProgressDto(int Done, int Total);

/// <summary>Quién pidió una corrida: la persona (con su nombre) o el proceso. (nuevo)</summary>
public sealed record ProcessRunRequesterDto(string Kind, string? Name);

/// <summary>Una corrida en la lista (api.md §16.4), sin datos del deudor. (nuevo)</summary>
public sealed record ProcessRunListItemDto(
    Guid PublicId,
    string Kind,
    DateOnly? Date,
    string? Period,
    string Status,
    ProcessRunProgressDto Progress,
    ProcessRunRequesterDto RequestedBy,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    int Errors);

/// <summary>
/// <c>GET /api/lending/process-runs</c> (feature 013, US4, T0499; api.md §16.4, <c>Lending.Processes.View</c>): las corridas por clase, estado y
/// fechas, la más reciente primero, con su avance y sus errores contados. Sin datos del deudor (es <c>View</c>). (nuevo)
/// </summary>
public sealed record ListLendingProcessRunsQuery(
    LendingProcessKind? Kind = null,
    LendingProcessRunStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ProcessRunListItemDto>>>;

public sealed class ListLendingProcessRunsQueryValidator : AbstractValidator<ListLendingProcessRunsQuery>
{
    public ListLendingProcessRunsQueryValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListLendingProcessRunsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListLendingProcessRunsQuery, Result<PagedResult<ProcessRunListItemDto>>>
{
    public async Task<Result<PagedResult<ProcessRunListItemDto>>> Handle(ListLendingProcessRunsQuery request, CancellationToken ct)
    {
        var consulta = db.LendingProcessRuns.AsNoTracking().Where(r => !r.IsDeleted);
        if (request.Kind is { } clase) consulta = consulta.Where(r => r.Kind == clase);
        if (request.Status is { } estado) consulta = consulta.Where(r => r.Status == estado);
        if (request.From is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(r => r.RequestedAt >= inicio);
        }
        if (request.To is { } hasta)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(r => r.RequestedAt < fin);
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ids = filas.Select(r => r.Id).ToList();
        var errores = await db.LendingProcessRunErrors.AsNoTracking().Where(e => ids.Contains(e.ProcessRunId))
            .GroupBy(e => e.ProcessRunId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var items = filas.Select(r => Item(r, errores.GetValueOrDefault(r.Id))).ToList();
        return Result.Success(new PagedResult<ProcessRunListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    public static ProcessRunListItemDto Item(LendingProcessRun r, int errores) => new(
        r.PublicId,
        r.Kind.ToString(),
        r.ProcessDate,
        r.PeriodYear is { } a && r.PeriodMonth is { } m ? $"{a:D4}-{m:D2}" : null,
        r.Status.ToString(),
        new ProcessRunProgressDto(r.ProcessedLoans + r.FailedLoans + r.SkippedLoans, r.TotalLoans),
        new ProcessRunRequesterDto(r.RequestedByKind.ToString(), r.RequestedByName),
        r.StartedAt,
        r.FinishedAt,
        errores);
}
