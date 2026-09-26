using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>Los impuestos y retenciones (feature 012, T165; contracts/api.md §30, <c>GET /api/core/taxes?kind=&amp;active=</c>). (nuevo)</summary>
public sealed record ListTaxDefinitionsQuery(TaxKind? Kind = null, bool? Active = null) : IRequest<Result<IReadOnlyList<TaxDefinitionDto>>>;

public sealed class ListTaxDefinitionsQueryValidator : AbstractValidator<ListTaxDefinitionsQuery>
{
    public ListTaxDefinitionsQueryValidator() => RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
}

public sealed class ListTaxDefinitionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListTaxDefinitionsQuery, Result<IReadOnlyList<TaxDefinitionDto>>>
{
    public async Task<Result<IReadOnlyList<TaxDefinitionDto>>> Handle(ListTaxDefinitionsQuery request, CancellationToken ct)
    {
        var q = db.TaxDefinitions.AsNoTracking().Include(t => t.TaxedOnDefinition).AsQueryable();
        if (request.Kind is { } clase) q = q.Where(t => t.Kind == clase);
        if (request.Active is { } activo) q = q.Where(t => t.IsActive == activo);
        var lista = await q.OrderBy(t => t.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TaxDefinitionDto>>(lista.Select(TaxMapper.Definicion).ToList());
    }
}

/// <summary>Un impuesto con todas sus vigencias (§30, <c>GET /api/core/taxes/{id}</c>). (nuevo)</summary>
public sealed record GetTaxDefinitionQuery(Guid TaxPublicId) : IRequest<Result<TaxDefinitionDto>>;

public sealed class GetTaxDefinitionQueryValidator : AbstractValidator<GetTaxDefinitionQuery>
{
    public GetTaxDefinitionQueryValidator() => RuleFor(x => x.TaxPublicId).NotEmpty();
}

public sealed class GetTaxDefinitionQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTaxDefinitionQuery, Result<TaxDefinitionDto>>
{
    public async Task<Result<TaxDefinitionDto>> Handle(GetTaxDefinitionQuery request, CancellationToken ct)
    {
        var impuesto = await db.TaxDefinitions.AsNoTracking().Include(t => t.TaxedOnDefinition)
            .FirstOrDefaultAsync(t => t.PublicId == request.TaxPublicId, ct);
        if (impuesto is null) return Result.Failure<TaxDefinitionDto>(TaxErrors.TaxNotFound());

        var tarifas = await db.TaxRates.AsNoTracking().Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept)
            .Where(r => r.TaxDefinitionId == impuesto.Id)
            .OrderBy(r => r.Code).ThenBy(r => r.ValidFrom).ToListAsync(ct);
        return Result.Success(TaxMapper.Definicion(impuesto) with { Rates = tarifas.Select(TaxMapper.Tarifa).ToList() });
    }
}

/// <summary>
/// Las tarifas (§30, <c>GET /api/core/tax-rates?tax=&amp;asOf=&amp;municipality=&amp;reviewPending=&amp;onlyCurrent=</c>).
/// <c>asOf</c> filtra las vigentes a esa fecha; <c>onlyCurrent</c>, las vigentes hoy. (nuevo)
/// </summary>
public sealed record ListTaxRatesQuery(
    Guid? TaxPublicId = null,
    DateOnly? AsOf = null,
    string? Municipality = null,
    bool? ReviewPending = null,
    bool? OnlyCurrent = null) : IRequest<Result<IReadOnlyList<TaxRateDto>>>;

public sealed class ListTaxRatesQueryValidator : AbstractValidator<ListTaxRatesQuery>
{
    public ListTaxRatesQueryValidator() => RuleFor(x => x.Municipality).MaximumLength(5);
}

public sealed class ListTaxRatesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListTaxRatesQuery, Result<IReadOnlyList<TaxRateDto>>>
{
    public async Task<Result<IReadOnlyList<TaxRateDto>>> Handle(ListTaxRatesQuery request, CancellationToken ct)
    {
        var q = db.TaxRates.AsNoTracking().Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept).AsQueryable();
        if (request.TaxPublicId is { } impuesto) q = q.Where(r => r.TaxDefinition!.PublicId == impuesto);
        if (!string.IsNullOrWhiteSpace(request.Municipality)) q = q.Where(r => r.MunicipalityDaneCode == request.Municipality.Trim());
        if (request.ReviewPending is { } pendiente) q = q.Where(r => r.ReviewPending == pendiente);
        var fecha = request.AsOf ?? (request.OnlyCurrent == true ? reloj.HoyLocal : (DateOnly?)null);
        if (fecha is { } f) q = q.Where(r => r.ValidFrom <= f && (r.ValidTo == null || r.ValidTo >= f));

        var lista = await q.OrderBy(r => r.TaxDefinition!.Code).ThenBy(r => r.Code).ThenBy(r => r.ValidFrom).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TaxRateDto>>(lista.Select(TaxMapper.Tarifa).ToList());
    }
}

/// <summary>Una tarifa con las otras vigencias de su código (§30, <c>GET /api/core/tax-rates/{id}</c>). (nuevo)</summary>
public sealed record GetTaxRateQuery(Guid TaxRatePublicId) : IRequest<Result<TaxRateDto>>;

public sealed class GetTaxRateQueryValidator : AbstractValidator<GetTaxRateQuery>
{
    public GetTaxRateQueryValidator() => RuleFor(x => x.TaxRatePublicId).NotEmpty();
}

public sealed class GetTaxRateQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTaxRateQuery, Result<TaxRateDto>>
{
    public async Task<Result<TaxRateDto>> Handle(GetTaxRateQuery request, CancellationToken ct)
    {
        var tarifa = await db.TaxRates.AsNoTracking().Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept)
            .FirstOrDefaultAsync(r => r.PublicId == request.TaxRatePublicId, ct);
        if (tarifa is null) return Result.Failure<TaxRateDto>(TaxErrors.RateNotFound());

        var otras = await db.TaxRates.AsNoTracking().Include(r => r.TaxDefinition).Include(r => r.WithholdingConcept)
            .Where(r => r.Code == tarifa.Code && r.Id != tarifa.Id).OrderBy(r => r.ValidFrom).ToListAsync(ct);
        return Result.Success(TaxMapper.Tarifa(tarifa) with { OtherVersions = otras.Select(TaxMapper.Tarifa).ToList() });
    }
}

/// <summary>Los conceptos de retención (§30, <c>GET /api/core/withholding-concepts?active=</c>). (nuevo)</summary>
public sealed record ListWithholdingConceptsQuery(bool? Active = null) : IRequest<Result<IReadOnlyList<WithholdingConceptDto>>>;

public sealed class ListWithholdingConceptsQueryValidator : AbstractValidator<ListWithholdingConceptsQuery>
{
    public ListWithholdingConceptsQueryValidator() => RuleFor(x => x.Active).Must(_ => true);
}

public sealed class ListWithholdingConceptsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListWithholdingConceptsQuery, Result<IReadOnlyList<WithholdingConceptDto>>>
{
    public async Task<Result<IReadOnlyList<WithholdingConceptDto>>> Handle(ListWithholdingConceptsQuery request, CancellationToken ct)
    {
        IQueryable<WithholdingConcept> q = db.WithholdingConcepts.AsNoTracking();
        if (request.Active is { } activo) q = q.Where(c => c.IsActive == activo);
        var lista = await q.OrderBy(c => c.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<WithholdingConceptDto>>(lista.Select(TaxMapper.Concepto).ToList());
    }
}
