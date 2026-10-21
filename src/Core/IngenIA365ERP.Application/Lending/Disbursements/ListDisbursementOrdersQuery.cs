using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Las órdenes de giro (feature 013, US3, T0445; api.md §9.2, <c>GET /api/lending/disbursement-orders</c>, <c>Lending.Loans.Read</c>): por estado,
/// crédito, fechas de emisión y días sin resultado, con la cuenta <b>enmascarada</b>; filtra en silencio por el alcance de oficina y línea del
/// crédito (<c>AlcanceDeCartera</c>). (nuevo)
/// </summary>
public sealed record ListDisbursementOrdersQuery(
    DisbursementOrderStatus? Status = null,
    Guid? LoanPublicId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int? WithoutResultDays = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<DisbursementOrderDto>>>;

public sealed class ListDisbursementOrdersQueryValidator : AbstractValidator<ListDisbursementOrdersQuery>
{
    public ListDisbursementOrdersQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.WithoutResultDays).GreaterThanOrEqualTo(0).When(x => x.WithoutResultDays is not null);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListDisbursementOrdersQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListDisbursementOrdersQuery, Result<PagedResult<DisbursementOrderDto>>>
{
    public async Task<Result<PagedResult<DisbursementOrderDto>>> Handle(ListDisbursementOrdersQuery request, CancellationToken ct)
    {
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.LoanPublicId is { } credito) creditos = creditos.Where(l => l.PublicId == credito);
        var consulta = db.DisbursementOrders.AsNoTracking().Where(o => !o.IsDeleted && creditos.Any(l => l.Id == o.LoanId));
        if (request.Status is { } estado) consulta = consulta.Where(o => o.Status == estado);
        if (request.From is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(o => o.IssuedAt >= inicio);
        }
        if (request.To is { } hasta)
        {
            var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(o => o.IssuedAt < fin);
        }
        if (request.WithoutResultDays is { } dias)
        {
            var limite = reloj.HoyLocal.AddDays(-dias);
            consulta = consulta.Where(o => o.Status == DisbursementOrderStatus.Issued && o.ResultDueOn <= limite);
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(o => o.IssuedAt)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var items = await ConsultasDeOrdenesDeGiro.DtosAsync(db, filas, cuentaCompleta: false, ct);
        return Result.Success(new PagedResult<DisbursementOrderDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>El DTO de las órdenes de giro (lo comparten la lista, el detalle y los comandos de la orden). (nuevo)</summary>
public static class ConsultasDeOrdenesDeGiro
{
    public static async Task<IReadOnlyList<DisbursementOrderDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<DisbursementOrder> ordenes,
        bool cuentaCompleta, CancellationToken ct)
    {
        if (ordenes.Count == 0) return [];
        var creditosIds = ordenes.Select(o => o.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => creditosIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var operacionesIds = ordenes.Select(o => o.DisbursementOperationId).ToList();
        var tramos = await db.LoanOperations.AsNoTracking().Where(o => operacionesIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.TrancheNumber ?? 1, ct);
        var personasIds = ordenes.Select(o => o.BeneficiaryPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personasIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var bancosIds = ordenes.Where(o => o.DestinationBankId is not null).Select(o => o.DestinationBankId!.Value).Distinct().ToList();
        var bancos = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => bancosIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var archivosIds = ordenes.Where(o => o.CurrentDisbursementFileId is not null).Select(o => o.CurrentDisbursementFileId!.Value).ToList();
        var archivos = await db.DisbursementFiles.AsNoTracking().Where(f => archivosIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Number, ct);
        var anterioresIds = ordenes.Where(o => o.PreviousOrderId is not null).Select(o => o.PreviousOrderId!.Value).ToList();
        var anteriores = await db.DisbursementOrders.AsNoTracking().Where(o => anterioresIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        var ids = ordenes.Select(o => o.Id).ToList();
        var resultados = (await db.DisbursementOrderResults.AsNoTracking().Where(r => ids.Contains(r.OrderId)).OrderBy(r => r.RegisteredAt).ToListAsync(ct))
            .ToLookup(r => r.OrderId);

        return ordenes.Select(o => new DisbursementOrderDto(
            o.PublicId,
            o.Number,
            creditos[o.LoanId].PublicId,
            creditos[o.LoanId].Number,
            tramos.GetValueOrDefault(o.DisbursementOperationId, 1),
            o.Form.ToString(),
            new DisbursementBeneficiaryDto(personas.GetValueOrDefault(o.BeneficiaryPersonId), o.BeneficiaryName, o.BeneficiaryTaxId,
                o.DestinationBankId is { } b ? bancos.GetValueOrDefault(b) : null, BeneficiaryInput.Texto(o.DestinationAccountType),
                cuentaCompleta ? o.DestinationAccountNumber : Enmascarar(o.DestinationAccountNumber)),
            o.GrossAmount,
            o.PayoffAmount,
            o.NetAmount,
            o.IssuedAt,
            o.ResultDueOn,
            o.Status.ToString(),
            o.AnulacionPedida,
            o.VoidReason,
            o.PaidByFile,
            o.CurrentDisbursementFileId is { } f ? archivos.GetValueOrDefault(f) : null,
            o.PreviousOrderId is { } p ? anteriores.GetValueOrDefault(p) : null,
            resultados[o.Id].Select(r => new DisbursementOrderResultDto(r.Result.ToString(), r.TreasuryReference, r.ResultDate, r.RegisteredAt, r.Reason,
                r.ReversalOperationId is not null)).ToList())).ToList();
    }

    /// <summary>La cuenta con sólo sus últimos cuatro dígitos a la vista.</summary>
    public static string? Enmascarar(string? cuenta) =>
        string.IsNullOrEmpty(cuenta) ? cuenta : cuenta.Length <= 4 ? new string('*', cuenta.Length) : new string('*', cuenta.Length - 4) + cuenta[^4..];
}
