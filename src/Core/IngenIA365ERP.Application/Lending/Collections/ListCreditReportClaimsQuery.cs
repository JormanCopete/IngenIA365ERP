using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>El crédito de un reclamo, por su número (api.md §1.7).</summary>
public sealed record CreditoDelReclamoDto(Guid LoanPublicId, string Number);

/// <summary>Un reclamo o una suplantación sobre el reporte a centrales (api.md §14.4).</summary>
public sealed record CreditReportClaimDto(
    Guid PublicId,
    PersonaRefDto Person,
    CreditoDelReclamoDto? Loan,
    CreditReportClaimKind Kind,
    CreditReportClaimStatus Status,
    string? BureauCode,
    DateOnly ReceivedAt,
    DateOnly DueDate,
    bool Extended);

/// <summary>
/// Los reclamos sobre el reporte a centrales (feature 013, T0301, T69, C88; api.md §14.4,
/// <c>GET /api/lending/credit-report-claims?status=&amp;personPublicId=&amp;page=</c>, <c>Lending.CreditReportClaims.Read</c>). En K1 es
/// <b>sólo lectura</b> sobre <c>LND_CreditReportClaims</c> —lo migrado—; registrar, prorrogar, responder y retirar llegan con K4. Respeta
/// el alcance por la <b>oficina del crédito</b> (<see cref="IAlcanceDeCartera"/>, filtro en silencio); un reclamo sin crédito sólo lo ve
/// quien tiene todas las oficinas (falla cerrado). El vencimiento es el prorrogado si lo hay. El más reciente primero. (nuevo)
/// </summary>
public sealed record ListCreditReportClaimsQuery(
    CreditReportClaimStatus? Status = null,
    Guid? PersonPublicId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<CreditReportClaimDto>>>;

public sealed class ListCreditReportClaimsQueryValidator : AbstractValidator<ListCreditReportClaimsQuery>
{
    public ListCreditReportClaimsQueryValidator() => RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
}

public sealed class ListCreditReportClaimsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListCreditReportClaimsQuery, Result<PagedResult<CreditReportClaimDto>>>
{
    public async Task<Result<PagedResult<CreditReportClaimDto>>> Handle(ListCreditReportClaimsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = from c in db.CreditReportClaims.AsNoTracking()
                       join l in db.Loans.AsNoTracking() on c.LoanId equals (int?)l.Id into creditos
                       from l in creditos.DefaultIfEmpty()
                       where !c.IsDeleted
                       select new { Reclamo = c, Oficina = l == null ? (int?)null : l.BranchId, Numero = l == null ? null : l.Number, LoanPublicId = l == null ? (Guid?)null : l.PublicId };

        // El alcance de Cartera (FR-004): fuera de él, el reclamo no aparece ni cuenta. Sin crédito no hay oficina: sólo el alcance total.
        if (!alcance.TodasLasOficinas)
        {
            var oficinas = alcance.Oficinas.ToArray();
            consulta = consulta.Where(x => x.Oficina != null && oficinas.Contains(x.Oficina.Value));
        }
        if (request.Status is { } estado) consulta = consulta.Where(x => x.Reclamo.Status == estado);
        if (request.PersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(x => x.Reclamo.PersonId == id);
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.Reclamo.ReceivedOn).ThenByDescending(x => x.Reclamo.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(f => f.Reclamo.PersonId), ct);

        return Result.Success(new PagedResult<CreditReportClaimDto>(filas.Select(f => new CreditReportClaimDto(
            f.Reclamo.PublicId, personas[f.Reclamo.PersonId],
            f.LoanPublicId is { } loan ? new CreditoDelReclamoDto(loan, f.Numero!) : null,
            f.Reclamo.Kind, f.Reclamo.Status, f.Reclamo.BureauCode, f.Reclamo.ReceivedOn, f.Reclamo.ExtendedDueDate ?? f.Reclamo.DueDate,
            f.Reclamo.ExtendedDueDate is not null)).ToList(), pagina.SafePage, pagina.SafePageSize, total));
    }
}
