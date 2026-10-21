using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Las novedades (feature 013, K3, T0878; api.md §11.1, <c>GET /api/lending/novelties?status=&amp;cause=&amp;loanPublicId=&amp;page=</c>,
/// <c>Lending.Novelties.Read</c>): las de los créditos del alcance de oficina y línea (lo de fuera no aparece ni cuenta), las más recientes primero. (nuevo)
/// </summary>
public sealed record ListLoanNoveltiesQuery(LoanNoveltyStatus? Status = null, NoveltyCause? Cause = null, Guid? LoanPublicId = null, int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<LoanNoveltyListItemDto>>>;

public sealed class ListLoanNoveltiesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListLoanNoveltiesQuery, Result<PagedResult<LoanNoveltyListItemDto>>>
{
    public async Task<Result<PagedResult<LoanNoveltyListItemDto>>> Handle(ListLoanNoveltiesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        var consulta = from n in db.LoanNovelties.AsNoTracking()
                       join l in creditos on n.LoanId equals l.Id
                       where !n.IsDeleted
                       select n;
        if (request.Status is { } estado) consulta = consulta.Where(n => n.Status == estado);
        if (request.Cause is { } causa) consulta = consulta.Where(n => n.Cause == causa);
        if (request.LoanPublicId is { } credito)
            consulta = consulta.Where(n => db.Loans.Any(l => l.Id == n.LoanId && l.PublicId == credito)
                                           || db.LoanNoveltyLoans.Any(x => x.LoanNoveltyId == n.Id && db.Loans.Any(l => l.Id == x.LoanId && l.PublicId == credito)));
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(n => n.RequestedAt).ThenByDescending(n => n.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ids = filas.Select(n => n.Id).ToList();
        var creditosDe = (await (from x in db.LoanNoveltyLoans.AsNoTracking()
                                 join l in db.Loans.AsNoTracking() on x.LoanId equals l.Id
                                 where ids.Contains(x.LoanNoveltyId) && !x.IsDeleted
                                 select new { x.LoanNoveltyId, Dto = new NoveltyLoanDto(l.PublicId, l.Number, x.Role) }).ToListAsync(ct))
            .GroupBy(x => x.LoanNoveltyId).ToDictionary(g => g.Key, g => (IReadOnlyList<NoveltyLoanDto>)g.Select(x => x.Dto).OrderBy(d => d.Role).ToList());
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, filas.Select(n => n.RequestedByUserId), ct);
        var items = filas.Select(n => new LoanNoveltyListItemDto(n.PublicId, n.Number, creditosDe.GetValueOrDefault(n.Id) ?? [], n.Mechanism, n.Cause, n.Status,
            usuarios.GetValueOrDefault(n.RequestedByUserId)?.Name ?? string.Empty, n.RequestedAt,
            SimulacionGuardada.Leer(n.SimulationJson)?.RoutingResult?.LevelName)).ToList();
        return Result.Success(new PagedResult<LoanNoveltyListItemDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>La novedad completa (feature 013, K3, T0878; api.md §11.1, <c>GET /api/lending/novelties/{id}</c>, <c>Lending.Novelties.Read</c>). (nuevo)</summary>
public sealed record GetLoanNoveltyQuery(Guid NoveltyPublicId) : IRequest<Result<LoanNoveltyDto>>;

public sealed class GetLoanNoveltyQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetLoanNoveltyQuery, Result<LoanNoveltyDto>>
{
    public async Task<Result<LoanNoveltyDto>> Handle(GetLoanNoveltyQuery request, CancellationToken ct)
    {
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: false, ct);
        return leida.IsFailure ? Result.Failure<LoanNoveltyDto>(leida.Error) : Result.Success(await NovedadesDeCartera.DtoAsync(db, leida.Value.Novedad, ct));
    }
}

/// <summary>
/// Anula una novedad que no se aplicó (feature 013, K3, T0878; api.md §11.1, <c>POST /api/lending/novelties/{id}/cancel</c>,
/// <c>Lending.Novelties.Request</c>, con clave y motivo): en borrador, radicada o aprobada; aplicada, <c>Lending.Novelty.AlreadyApplied</c>. (nuevo)
/// </summary>
public sealed record CancelLoanNoveltyCommand(Guid NoveltyPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CancelLoanNoveltyCommandValidator : AbstractValidator<CancelLoanNoveltyCommand>
{
    public CancelLoanNoveltyCommandValidator()
    {
        RuleFor(x => x.NoveltyPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula la novedad.").MaximumLength(500);
    }
}

public sealed class CancelLoanNoveltyCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<CancelLoanNoveltyCommand, Result>
{
    public async Task<Result> Handle(CancelLoanNoveltyCommand request, CancellationToken ct)
    {
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure(leida.Error);
        try
        {
            leida.Value.Novedad.Cancelar(reloj.UtcNow, request.Reason);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
