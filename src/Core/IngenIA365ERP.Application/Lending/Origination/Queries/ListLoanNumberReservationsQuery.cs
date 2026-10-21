using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.Queries;

/// <summary>El pago de Comercial que reservó el número (api.md §7.3, <c>paymentReference</c>). (nuevo)</summary>
public sealed record ReservationPaymentReferenceDto(string Module, Guid PaymentPublicId, Guid SalesDocumentPublicId);

/// <summary>Una reserva de número (api.md §7.3). (nuevo)</summary>
public sealed record LoanNumberReservationDto(
    Guid PublicId,
    string LoanNumber,
    string Status,
    decimal Amount,
    RefDto Person,
    ReservationPaymentReferenceDto PaymentReference,
    DateTime ReservedAt,
    DateOnly ExpiresAt,
    Guid? UsedByLoanPublicId,
    DateTime? ReleasedAt,
    string? ReleaseReason,
    string? ConsultationEvidence);

/// <summary>
/// <c>GET /api/lending/number-reservations?status=&amp;number=&amp;from=&amp;to=&amp;page=</c> (feature 013, US6, T0596; api.md §7.3; FR-057, T22;
/// <c>Lending.NumberReservations.Read</c>): las reservas de número que Comercial pidió dentro de sus confirmaciones —aquí sólo se consultan—: número,
/// estado, valor, persona, pago de Comercial, vencimiento y el crédito que lo usó. Una reserva usada sale con la oficina y la línea de su crédito y
/// respeta el alcance (<c>LasConsultasDeCarteraRespetanElAlcance</c>); una sin crédito todavía no es de ninguna oficina. (nuevo)
/// </summary>
public sealed record ListLoanNumberReservationsQuery(
    LoanNumberReservationStatus? Status = null,
    string? Number = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<LoanNumberReservationDto>>>;

public sealed class ListLoanNumberReservationsQueryValidator : AbstractValidator<ListLoanNumberReservationsQuery>
{
    public ListLoanNumberReservationsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Number).MaximumLength(20);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ListLoanNumberReservationsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListLoanNumberReservationsQuery, Result<PagedResult<LoanNumberReservationDto>>>
{
    public async Task<Result<PagedResult<LoanNumberReservationDto>>> Handle(ListLoanNumberReservationsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var enAlcance = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.LoanNumberReservations.AsNoTracking().Where(r => !r.IsDeleted && (r.LoanId == null || enAlcance.Contains(r.LoanId.Value)));
        if (request.Status is { } estado) consulta = consulta.Where(r => r.Status == estado);
        if (!string.IsNullOrWhiteSpace(request.Number)) consulta = consulta.Where(r => r.Number == request.Number.Trim());
        if (request.From is { } desde)
        {
            var d = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(r => r.ReservedAt >= d);
        }
        if (request.To is { } hasta)
        {
            var h = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(r => r.ReservedAt < h);
        }

        var total = await consulta.CountAsync(ct);
        var pagina = await consulta.OrderByDescending(r => r.ReservedAt).ThenByDescending(r => r.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var personas = pagina.Select(r => r.PersonId).Distinct().ToList();
        var nombres = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToListAsync(ct);
        var creditos = pagina.Where(r => r.LoanId is not null).Select(r => r.LoanId!.Value).Distinct().ToList();
        var deCredito = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.PublicId, ct);
        var dtos = pagina.Select(r =>
        {
            var p = nombres.First(x => x.Id == r.PersonId);
            return new LoanNumberReservationDto(r.PublicId, r.Number, r.Status.ToString(), r.Amount, new RefDto(p.PublicId, ReferenciasDeGobierno.Nombre(p)),
                new ReservationPaymentReferenceDto("INV", r.PaymentPublicId, r.SalesDocumentPublicId), r.ReservedAt, r.ExpiresOn,
                r.LoanId is { } l ? deCredito.GetValueOrDefault(l) : null, r.ReleasedAt, r.ReleaseReason, r.ConsultationEvidence);
        }).ToList();
        return Result.Success(new PagedResult<LoanNumberReservationDto>(dtos, request.Page, request.PageSize, total));
    }
}
