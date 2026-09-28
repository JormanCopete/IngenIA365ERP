using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MedioDePago = IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

/// <summary>
/// Los medios de pago (feature 012, I3, T590; contracts/api.md §22.1, <c>GET /api/core/payment-means?class=&amp;active=&amp;asOf=</c>).
/// <c>asOf</c> deja sólo los vigentes a esa fecha. Por orden y código. (nuevo)
/// </summary>
public sealed record ListPaymentMeansQuery(PaymentMeansClass? Class = null, bool? Active = null, DateOnly? AsOf = null)
    : IRequest<Result<IReadOnlyList<PaymentMeansDto>>>;

public sealed class ListPaymentMeansQueryValidator : AbstractValidator<ListPaymentMeansQuery>
{
    public ListPaymentMeansQueryValidator() => RuleFor(x => x.Class).IsInEnum().When(x => x.Class is not null);
}

public sealed class ListPaymentMeansQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPaymentMeansQuery, Result<IReadOnlyList<PaymentMeansDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentMeansDto>>> Handle(ListPaymentMeansQuery request, CancellationToken ct)
    {
        var q = db.PaymentMeans.AsNoTracking().Where(m => !m.IsDeleted);
        if (request.Class is { } clase) q = q.Where(m => m.Class == clase);
        if (request.Active is { } activo) q = q.Where(m => m.IsActive == activo);
        if (request.AsOf is { } fecha) q = q.Where(m => m.ValidFrom <= fecha && (m.ValidTo == null || m.ValidTo >= fecha));
        var medios = await q.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Code).ToListAsync(ct);
        return Result.Success(await VistaDeMediosDePago.VariosAsync(db, medios, ct));
    }
}

/// <summary>Un medio con cuántos pagos tiene y dónde se ofrece (§22.1, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetPaymentMeansQuery(Guid PaymentMeansPublicId) : IRequest<Result<PaymentMeansDto>>;

public sealed class GetPaymentMeansQueryValidator : AbstractValidator<GetPaymentMeansQuery>
{
    public GetPaymentMeansQueryValidator() => RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
}

public sealed class GetPaymentMeansQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPaymentMeansQuery, Result<PaymentMeansDto>>
{
    public async Task<Result<PaymentMeansDto>> Handle(GetPaymentMeansQuery request, CancellationToken ct)
    {
        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<PaymentMeansDto>(PaymentMeansErrors.NotFound());
        return Result.Success(await VistaDeMediosDePago.UnoAsync(db, medio, detalle: true, ct));
    }
}

/// <summary>Arma <see cref="PaymentMeansDto"/> con los códigos citados en bloque (nunca una consulta por medio). (nuevo)</summary>
public static class VistaDeMediosDePago
{
    public static async Task<PaymentMeansDto> UnoAsync(IApplicationDbContext db, MedioDePago medio, bool detalle, CancellationToken ct)
    {
        var dto = (await VariosAsync(db, [medio], ct))[0];
        if (!detalle) return dto;
        return dto with
        {
            PaymentsCount = await ReglasDeMedioDePago.PagosAsync(db, medio.Id, ct),
            Availability = await Inventory.Pos.DisponibilidadDeMedioEnBase.LeerAsync(db, medio, null, ct),
        };
    }

    public static async Task<IReadOnlyList<PaymentMeansDto>> VariosAsync(IApplicationDbContext db, IReadOnlyList<MedioDePago> medios, CancellationToken ct)
    {
        var redesIds = medios.Where(m => m.CardNetworkId is not null).Select(m => m.CardNetworkId!.Value).Distinct().ToList();
        var adqIds = medios.Where(m => m.CardAcquirerId is not null).Select(m => m.CardAcquirerId!.Value).Distinct().ToList();
        var bancosIds = medios.Where(m => m.BankId is not null).Select(m => m.BankId!.Value).Distinct().ToList();
        var redes = await db.CardNetworks.AsNoTracking().Where(n => redesIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => (n.PublicId, n.Code), ct);
        var adqs = await db.CardAcquirers.AsNoTracking().Where(a => adqIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => (a.PublicId, a.Code), ct);
        var bancos = await db.Banks.AsNoTracking().Where(b => bancosIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);

        return medios.Select(m =>
        {
            (Guid PublicId, string Code)? red = m.CardNetworkId is { } r && redes.TryGetValue(r, out var rv) ? rv : null;
            (Guid PublicId, string Code)? adq = m.CardAcquirerId is { } a && adqs.TryGetValue(a, out var av) ? av : null;
            Guid? banco = m.BankId is { } b && bancos.TryGetValue(b, out var bv) ? bv : null;
            var credito = m.DefaultTermDays is null && m.MaxInstallments is null ? null
                : new CreditDefaultsDto(m.DefaultTermDays, m.MaxTermDays, m.MaxInstallments, m.DefaultInstallments, m.InstallmentPeriodDays, m.SuggestedCreditLineCode);
            return new PaymentMeansDto(
                m.PublicId, m.Code, m.Name, m.DisplayOrder, m.QuickKey, m.Class,
                red?.PublicId, red?.Code, adq?.PublicId, adq?.Code, banco, m.DestinationAccountNumber, m.DestinationAccountType,
                m.RequiresReference, m.ReferenceKind, m.ReferenceMinLength, m.ReferenceMaxLength, m.AllowsChange, m.AllowsPartial, m.UniqueReference,
                m.CountMethod, m.RequiresTerminalBatchAtClose, m.ToleranceAmount, m.ExpectedCommissionRate, m.ExpectedCommissionFixed, m.DianPaymentMeansCode,
                credito, m.OfferedAtAllPointsOfSale, m.OfferedInAllChannels, m.OfferedForAllDocumentTypes, m.IsActive, m.ValidFrom, m.ValidTo, m.Notes);
        }).ToList();
    }
}
