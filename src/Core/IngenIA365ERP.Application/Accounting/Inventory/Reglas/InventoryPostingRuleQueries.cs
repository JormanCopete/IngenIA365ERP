using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

// Consultas de la matriz contable de Inventario (feature 012, T508; api.md §26.1): el catálogo para armar una regla, la
// lista paginada con sus filtros, el detalle y las versiones de una clave. Sin permisos aquí: los pone la ruta (T528). (nuevo)

/// <summary>Una referencia de Core con su código (sucursal, centro de costo). (nuevo)</summary>
public sealed record ReferenciaDeReglaDto(Guid PublicId, string Code);

/// <summary>Las dimensiones de una regla guardada (nuevo).</summary>
public sealed record DimensionesDeReglaGuardadaDto(
    string? AccountingGroupCode,
    string? WarehouseCode,
    string? PointOfSaleCode,
    string? PaymentMeansCode,
    string? TaxRateCode,
    decimal? TaxRate,
    string? ReasonCode,
    ReferenciaDeReglaDto? Branch,
    ReferenciaDeReglaDto? CostCenter);

/// <summary>La cuenta de una regla y si hoy sigue siendo elegible para INV (<see cref="AccountEligibility.Reparo"/>). (nuevo)</summary>
public sealed record CuentaDeReglaDto(Guid PublicId, string Code, string Name, bool IsEligible);

/// <summary><c>InventoryPostingRuleDto</c> de api.md §26.1 (nuevo).</summary>
public sealed record InventoryPostingRuleDto(
    Guid RulePublicId,
    string Operation,
    string Role,
    DimensionesDeReglaGuardadaDto Dimensions,
    CuentaDeReglaDto Account,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Notes,
    string DimensionKey,
    short Specificity,
    bool IsCurrent,
    string? CreatedByName);

/// <summary>El catálogo para armar una regla (api.md §26.1, <c>GET /rules/catalog</c>). (nuevo)</summary>
public sealed record InventoryRulesCatalogDto(
    IReadOnlyList<OperacionDelCatalogoDto> Operations,
    IReadOnlyList<RolDelCatalogoDto> Roles,
    DimensionesDelCatalogoDto Dimensions);

public sealed record OperacionDelCatalogoDto(string Code, string Message, IReadOnlyList<string> DebitRoles, IReadOnlyList<string> CreditRoles, IReadOnlyList<string> RequiredRoles);

public sealed record RolDelCatalogoDto(string Code, IReadOnlyList<string> RequiredDimensions, IReadOnlyList<string> AllowedDimensions, IReadOnlyList<string> FixedReasons);

public sealed record DimensionesDelCatalogoDto(
    IReadOnlyList<CodigoDeDimensionDto> AccountingGroups,
    IReadOnlyList<BodegaDelCatalogoDto> Warehouses,
    IReadOnlyList<CodigoDeDimensionDto> PointsOfSale,
    IReadOnlyList<CodigoDeDimensionDto> PaymentMeans,
    IReadOnlyList<TarifaDelCatalogoDto> TaxRates,
    IReadOnlyList<CodigoDeDimensionDto> Reasons);

public sealed record BodegaDelCatalogoDto(string Code, string Name, string? BranchCode, WarehouseBehavior Behavior);

public sealed record TarifaDelCatalogoDto(string Code, string Name, string Kind, decimal? Rate, decimal? AmountPerUnit, DateOnly ValidFrom, DateOnly? ValidTo);

// ============================================================================================================ catálogo --

public sealed record GetInventoryRulesCatalogQuery : IRequest<Result<InventoryRulesCatalogDto>>;

public sealed class GetInventoryRulesCatalogQueryValidator : AbstractValidator<GetInventoryRulesCatalogQuery>;

public sealed class GetInventoryRulesCatalogQueryHandler(IApplicationDbContext db, IDimensionesDeInventario dimensiones)
    : IRequestHandler<GetInventoryRulesCatalogQuery, Result<InventoryRulesCatalogDto>>
{
    public async Task<Result<InventoryRulesCatalogDto>> Handle(GetInventoryRulesCatalogQuery request, CancellationToken ct)
    {
        var catalogo = await dimensiones.CatalogoAsync(ct);
        var tarifas = await db.TaxRates.AsNoTracking().Where(t => !t.IsDeleted)
            .OrderBy(t => t.Code).ThenBy(t => t.ValidFrom)
            .Select(t => new TarifaDelCatalogoDto(t.Code, t.Name, t.TaxDefinition!.Kind.ToString(), t.Rate, t.AmountPerUnit, t.ValidFrom, t.ValidTo))
            .ToListAsync(ct);

        var operaciones = OperacionesDeInventario.Todas
            .Select(o => new OperacionDelCatalogoDto(o.Codigo, o.Mensaje, o.RolesDebito, o.RolesCredito, o.RolesExigidos)).ToList();
        var roles = RolesDeCuenta.Todos
            .Select(r => new RolDelCatalogoDto(r.Codigo,
                r.Exigidas.Select(RolesDeCuenta.NombreDe).ToList(),
                r.Exigidas.Concat(r.Opcionales).Select(RolesDeCuenta.NombreDe).ToList(),
                r.MotivosFijos))
            .ToList();

        // Los motivos: tratamientos y destinos de caja, razones del kardex y causas de ajuste de Inventario.
        var motivos = RolesDeCuenta.Todos.SelectMany(r => r.MotivosFijos)
            .Concat(RolesDeCuenta.RazonesDeAjusteDeCosto)
            .Distinct(StringComparer.Ordinal)
            .Select(m => new CodigoDeDimensionDto(m, m))
            .Concat(catalogo.AdjustmentCauses)
            .ToList();

        return Result.Success(new InventoryRulesCatalogDto(operaciones, roles, new DimensionesDelCatalogoDto(
            catalogo.AccountingGroups,
            catalogo.Warehouses.Select(w => new BodegaDelCatalogoDto(w.Code, w.Name, w.BranchCode, w.Behavior)).ToList(),
            catalogo.PointsOfSale,
            (catalogo.PaymentMeans ?? []).Select(m => new CodigoDeDimensionDto(m.Code, m.Name)).ToList(),
            tarifas,
            motivos)));
    }
}

// ================================================================================================================ lista --

/// <summary>Los filtros de api.md §26.1 (<c>GET /rules?operation=&amp;role=&amp;asOf=…&amp;onlyCurrent=</c>). (nuevo)</summary>
public sealed record ListInventoryPostingRulesQuery(
    string? Operation = null,
    string? Role = null,
    DateOnly? AsOf = null,
    string? AccountingGroupCode = null,
    string? WarehouseCode = null,
    string? PointOfSaleCode = null,
    string? PaymentMeansCode = null,
    string? TaxRateCode = null,
    string? ReasonCode = null,
    string? Account = null,
    bool OnlyCurrent = false,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<InventoryPostingRuleDto>>>;

public sealed class ListInventoryPostingRulesQueryValidator : AbstractValidator<ListInventoryPostingRulesQuery>
{
    public ListInventoryPostingRulesQueryValidator()
    {
        RuleFor(x => x.Operation).MaximumLength(40);
        RuleFor(x => x.Role).MaximumLength(40);
        RuleFor(x => x.Account).MaximumLength(30);
    }
}

public sealed class ListInventoryPostingRulesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListInventoryPostingRulesQuery, Result<PagedResult<InventoryPostingRuleDto>>>
{
    public async Task<Result<PagedResult<InventoryPostingRuleDto>>> Handle(ListInventoryPostingRulesQuery request, CancellationToken ct)
    {
        var pagina = request.Pagina ?? new PageRequest();
        var consulta = db.InventoryPostingRules.AsNoTracking().Where(r => !r.IsDeleted);

        if (Texto(request.Operation) is { } op) consulta = consulta.Where(r => r.Operation == op);
        if (Texto(request.Role) is { } rol) consulta = consulta.Where(r => r.Role == rol);
        if (Codigo(request.AccountingGroupCode) is { } g) consulta = consulta.Where(r => r.AccountingGroupCode == g);
        if (Codigo(request.WarehouseCode) is { } w) consulta = consulta.Where(r => r.WarehouseCode == w);
        if (Codigo(request.PointOfSaleCode) is { } p) consulta = consulta.Where(r => r.PointOfSaleCode == p);
        if (Codigo(request.PaymentMeansCode) is { } m) consulta = consulta.Where(r => r.PaymentMeansCode == m);
        if (Codigo(request.TaxRateCode) is { } t) consulta = consulta.Where(r => r.TaxRateCode == t);
        if (Texto(request.ReasonCode) is { } motivo) consulta = consulta.Where(r => r.ReasonCode == motivo);
        if (Texto(request.Account) is { } cuenta)
            consulta = consulta.Where(r => db.ChartOfAccounts.Any(a => a.Id == r.AccountId && a.Code.StartsWith(cuenta)));

        var fecha = request.AsOf ?? (request.OnlyCurrent ? reloj.HoyLocal : (DateOnly?)null);
        if (fecha is { } f) consulta = consulta.Where(r => r.ValidFrom <= f && (r.ValidTo == null || r.ValidTo >= f));

        var total = await consulta.LongCountAsync(ct);
        var reglas = await consulta
            .OrderBy(r => r.Operation).ThenBy(r => r.Role).ThenBy(r => r.DimensionKey).ThenByDescending(r => r.ValidFrom)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var items = await VistaDeReglas.AsDtoAsync(db, reglas, reloj.HoyLocal, ct);
        return Result.Success(new PagedResult<InventoryPostingRuleDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    private static string? Texto(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string? Codigo(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
}

// ======================================================================================================= detalle --

public sealed record GetInventoryPostingRuleQuery(Guid RulePublicId) : IRequest<Result<InventoryPostingRuleDto>>;

public sealed class GetInventoryPostingRuleQueryValidator : AbstractValidator<GetInventoryPostingRuleQuery>
{
    public GetInventoryPostingRuleQueryValidator() => RuleFor(x => x.RulePublicId).NotEmpty();
}

public sealed class GetInventoryPostingRuleQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetInventoryPostingRuleQuery, Result<InventoryPostingRuleDto>>
{
    public async Task<Result<InventoryPostingRuleDto>> Handle(GetInventoryPostingRuleQuery request, CancellationToken ct)
    {
        var regla = await db.InventoryPostingRules.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (regla is null) return Result.Failure<InventoryPostingRuleDto>(AccountingErrors.InventoryRuleNotFound);
        return Result.Success((await VistaDeReglas.AsDtoAsync(db, [regla], reloj.HoyLocal, ct))[0]);
    }
}

/// <summary>Las versiones de la misma <c>DimensionKey</c>, de la más nueva a la más vieja (<c>GET /rules/{id}/versions</c>). (nuevo)</summary>
public sealed record ListInventoryPostingRuleVersionsQuery(Guid RulePublicId) : IRequest<Result<IReadOnlyList<InventoryPostingRuleDto>>>;

public sealed class ListInventoryPostingRuleVersionsQueryValidator : AbstractValidator<ListInventoryPostingRuleVersionsQuery>
{
    public ListInventoryPostingRuleVersionsQueryValidator() => RuleFor(x => x.RulePublicId).NotEmpty();
}

public sealed class ListInventoryPostingRuleVersionsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListInventoryPostingRuleVersionsQuery, Result<IReadOnlyList<InventoryPostingRuleDto>>>
{
    public async Task<Result<IReadOnlyList<InventoryPostingRuleDto>>> Handle(ListInventoryPostingRuleVersionsQuery request, CancellationToken ct)
    {
        var clave = await db.InventoryPostingRules.AsNoTracking().Where(r => r.PublicId == request.RulePublicId && !r.IsDeleted)
            .Select(r => r.DimensionKey).FirstOrDefaultAsync(ct);
        if (clave is null) return Result.Failure<IReadOnlyList<InventoryPostingRuleDto>>(AccountingErrors.InventoryRuleNotFound);
        var versiones = await db.InventoryPostingRules.AsNoTracking().Where(r => r.DimensionKey == clave && !r.IsDeleted)
            .OrderByDescending(r => r.ValidFrom).ToListAsync(ct);
        return Result.Success(await VistaDeReglas.AsDtoAsync(db, versiones, reloj.HoyLocal, ct));
    }
}

/// <summary>De reglas a <see cref="InventoryPostingRuleDto"/> con sus cuentas, sucursales y centros en bloque. (nuevo)</summary>
internal static class VistaDeReglas
{
    public static async Task<IReadOnlyList<InventoryPostingRuleDto>> AsDtoAsync(
        IApplicationDbContext db, IReadOnlyList<InventoryPostingRule> reglas, DateOnly hoy, CancellationToken ct)
    {
        var idsCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var idsSucursal = reglas.Where(r => r.BranchId != null).Select(r => r.BranchId!.Value).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().Where(b => idsSucursal.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new ReferenciaDeReglaDto(b.PublicId, b.LegacyCode ?? b.Name), ct);
        var idsCentro = reglas.Where(r => r.CostCenterId != null).Select(r => r.CostCenterId!.Value).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking().Where(c => idsCentro.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new ReferenciaDeReglaDto(c.PublicId, c.LegacyCode ?? c.Name), ct);

        return reglas.Select(r =>
        {
            var cuenta = cuentas.GetValueOrDefault(r.AccountId);
            return new InventoryPostingRuleDto(
                r.PublicId, r.Operation, r.Role,
                new DimensionesDeReglaGuardadaDto(r.AccountingGroupCode, r.WarehouseCode, r.PointOfSaleCode, r.PaymentMeansCode, r.TaxRateCode,
                    r.TaxRate, r.ReasonCode,
                    r.BranchId is { } b ? sucursales.GetValueOrDefault(b) : null,
                    r.CostCenterId is { } c ? centros.GetValueOrDefault(c) : null),
                new CuentaDeReglaDto(cuenta?.PublicId ?? Guid.Empty, cuenta?.Code ?? string.Empty, cuenta?.Name ?? string.Empty,
                    AccountEligibility.Reparo(cuenta, ModuloContable.Inventario) is null),
                r.ValidFrom, r.ValidTo, r.Notes, r.DimensionKey, r.SpecificityWeight, r.VigenteEn(hoy), r.CreatedBy);
        }).ToList();
    }
}
