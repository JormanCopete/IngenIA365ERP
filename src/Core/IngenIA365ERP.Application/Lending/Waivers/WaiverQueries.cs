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

namespace IngenIA365ERP.Application.Lending.Waivers;

/// <summary>
/// Las condonaciones (feature 013, K3, T0884; api.md §11.2, <c>GET /api/lending/waivers?status=&amp;loanPublicId=&amp;page=</c>,
/// <c>Lending.Loans.Read</c>): las de los créditos del alcance de oficina y línea (lo de fuera no aparece ni cuenta), las más recientes primero. (nuevo)
/// </summary>
public sealed record ListWaiversQuery(WaiverStatus? Status = null, Guid? LoanPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<WaiverDto>>>;

public sealed class ListWaiversQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, CondonacionesDeCartera condonaciones)
    : IRequestHandler<ListWaiversQuery, Result<PagedResult<WaiverDto>>>
{
    public async Task<Result<PagedResult<WaiverDto>>> Handle(ListWaiversQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.LoanPublicId is { } credito) creditos = creditos.Where(l => l.PublicId == credito);
        var consulta = from w in db.Waivers.AsNoTracking().Include(x => x.Lines)
                       join l in creditos on w.LoanId equals l.Id
                       where !w.IsDeleted
                       select w;
        if (request.Status is { } estado) consulta = consulta.Where(w => w.Status == estado);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(w => w.RequestedAt).ThenByDescending(w => w.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var ids = filas.Select(w => w.LoanId).Distinct().ToList();
        var deLosCreditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var items = new List<WaiverDto>();
        foreach (var w in filas) items.Add(await condonaciones.DtoAsync(w, deLosCreditos[w.LoanId], ct));
        return Result.Success(new PagedResult<WaiverDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Una condonación (feature 013, K3, T0884; api.md §11.2, <c>GET /api/lending/waivers/{id}</c>, <c>Lending.Loans.Read</c>). (nuevo)</summary>
public sealed record GetWaiverQuery(Guid WaiverPublicId) : IRequest<Result<WaiverDto>>;

public sealed class GetWaiverQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, CondonacionesDeCartera condonaciones)
    : IRequestHandler<GetWaiverQuery, Result<WaiverDto>>
{
    public async Task<Result<WaiverDto>> Handle(GetWaiverQuery request, CancellationToken ct)
    {
        var leida = await CondonacionesDeCartera.LeerAsync(db, alcance, auditoria, request.WaiverPublicId, seguir: false, ct);
        return leida.IsFailure
            ? Result.Failure<WaiverDto>(leida.Error)
            : Result.Success(await condonaciones.DtoAsync(leida.Value.Condonacion, leida.Value.Credito, ct));
    }
}

/// <summary>
/// Anula una condonación que no se aplicó (feature 013, K3, T0884; api.md §11.2, <c>POST /api/lending/waivers/{id}/cancel</c>,
/// <c>Lending.Condonations.Request</c>, con clave y motivo): en borrador, radicada o aprobada. El motivo queda en la auditoría del comando (la condonación
/// no tiene columna para él). (nuevo)
/// </summary>
public sealed record CancelWaiverCommand(Guid WaiverPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CancelWaiverCommandValidator : AbstractValidator<CancelWaiverCommand>
{
    public CancelWaiverCommandValidator()
    {
        RuleFor(x => x.WaiverPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula la condonación.").MaximumLength(500);
    }
}

public sealed class CancelWaiverCommandHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<CancelWaiverCommand, Result>
{
    public async Task<Result> Handle(CancelWaiverCommand request, CancellationToken ct)
    {
        var leida = await CondonacionesDeCartera.LeerAsync(db, alcance, auditoria, request.WaiverPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure(leida.Error);
        try
        {
            leida.Value.Condonacion.Cancelar();
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
