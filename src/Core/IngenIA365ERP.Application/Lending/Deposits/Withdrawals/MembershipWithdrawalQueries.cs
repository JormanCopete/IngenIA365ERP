using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Withdrawals;

/// <summary>
/// <c>GET /api/lending/membership-withdrawals</c> (<c>ListMembershipWithdrawalsQuery</c>; api.md §11): los trámites de las cuentas de aportes en alcance,
/// por estado, causa y persona, los más recientes primero. Las listas filtran en silencio lo que está fuera del alcance. (nuevo)
/// </summary>
public sealed record ListMembershipWithdrawalsQuery(MembershipWithdrawalStatus? Status = null, MembershipWithdrawalCause? Cause = null,
    Guid? PersonPublicId = null, int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<MembershipWithdrawalDto>>>;

/// <summary><c>GET …/{id}</c> (<c>GetMembershipWithdrawalQuery</c>): el trámite con las líneas de su liquidación vigente. (nuevo)</summary>
public sealed record GetMembershipWithdrawalQuery(Guid WithdrawalPublicId) : IRequest<Result<MembershipWithdrawalDto>>;

public sealed class ListMembershipWithdrawalsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, RetirosDeAsociado retiros)
    : IRequestHandler<ListMembershipWithdrawalsQuery, Result<PagedResult<MembershipWithdrawalDto>>>
{
    public async Task<Result<PagedResult<MembershipWithdrawalDto>>> Handle(ListMembershipWithdrawalsQuery request, CancellationToken ct)
    {
        var vigente = await alcance.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().PorOficinaYLinea(vigente, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var consulta = db.MembershipWithdrawals.AsNoTracking()
            .Where(w => !w.IsDeleted && cuentas.Contains(w.ContributionAccountId)
                        && (request.Status == null || w.Status == request.Status)
                        && (request.Cause == null || w.Cause == request.Cause));
        if (request.PersonPublicId is { } persona)
        {
            var personas = db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => p.Id);
            consulta = consulta.Where(w => personas.Contains(w.PersonId));
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(w => w.Id).Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var items = new List<MembershipWithdrawalDto>(filas.Count);
        foreach (var w in filas) items.Add(await retiros.DtoAsync(w, conLineas: false, ct));
        return Result.Success(new PagedResult<MembershipWithdrawalDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

public sealed class GetMembershipWithdrawalQueryHandler(RetirosDeAsociado retiros, IAlcanceDeCartera alcance) : IRequestHandler<GetMembershipWithdrawalQuery, Result<MembershipWithdrawalDto>>
{
    public async Task<Result<MembershipWithdrawalDto>> Handle(GetMembershipWithdrawalQuery request, CancellationToken ct)
    {
        var leido = await retiros.EnAlcanceAsync(alcance, request.WithdrawalPublicId, seguir: false, ct);
        return leido.IsFailure
            ? Result.Failure<MembershipWithdrawalDto>(leido.Error)
            : Result.Success(await retiros.DtoAsync(leido.Value, conLineas: true, ct));
    }
}
