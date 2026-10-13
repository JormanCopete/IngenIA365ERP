using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

// Consultas de la matriz contable de Cartera (feature 013, T0642; contracts/contabilidad.md §3.7; api.md §23.1): el catálogo para armar una
// regla, la lista paginada con sus filtros, el detalle, las versiones de una clave y «¿a qué cuenta va esta combinación?». Sin permisos
// aquí: los pone la ruta (T0661). Molde: InventoryPostingRuleQueries (012). (nuevo)

/// <summary>Una referencia de Core con su código (sucursal, centro de costo). (nuevo)</summary>
public sealed record ReferenciaDeReglaDeCarteraDto(Guid PublicId, string Code);

/// <summary>Las dimensiones de una regla guardada. (nuevo)</summary>
public sealed record DimensionesDeReglaDeCarteraGuardadaDto(
    string? CreditLineCode,
    string? ModalityCode,
    string? SubsegmentCode,
    string? RiskCategory,
    string? ConceptCode,
    bool? IsEmployee,
    bool? InGracePeriod,
    bool? OnBehalfOfAssociate,
    bool? Restructured,
    string? CollectorClass,
    string? ReasonCode,
    ReferenciaDeReglaDeCarteraDto? Branch,
    ReferenciaDeReglaDeCarteraDto? CostCenter);

/// <summary>La cuenta de una regla y si hoy sigue siendo elegible para CAR (<see cref="AccountEligibility.Reparo"/>). (nuevo)</summary>
public sealed record CuentaDeReglaDeCarteraDto(Guid PublicId, string Code, string Name, bool IsEligible);

/// <summary><c>LendingPostingRuleDto</c> de api.md §23.1. (nuevo)</summary>
public sealed record LendingPostingRuleDto(
    Guid RulePublicId,
    string Operation,
    string Role,
    DimensionesDeReglaDeCarteraGuardadaDto Dimensions,
    CuentaDeReglaDeCarteraDto Account,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Notes,
    string DimensionKey,
    int SpecificityWeight,
    bool IsCurrent,
    string? CreatedByName);

/// <summary>El catálogo para armar una regla (<c>GET /rules/catalog</c>). (nuevo)</summary>
public sealed record LendingRulesCatalogDto(
    IReadOnlyList<HechoDelCatalogoDeCarteraDto> Operations,
    IReadOnlyList<RolDelCatalogoDeCarteraDto> Roles,
    DimensionesDelCatalogoDeCarteraDto Dimensions);

public sealed record HechoDelCatalogoDeCarteraDto(
    string Code, string ShortCode, bool IsInformational, IReadOnlyList<string> DebitRoles, IReadOnlyList<string> CreditRoles,
    string DefaultMode, string? DefaultVoucherType, string Delivery);

public sealed record RolDelCatalogoDeCarteraDto(
    string Code, string Nature, string Family, IReadOnlyList<string> RequiredDimensions, IReadOnlyList<string> AllowedDimensions,
    IReadOnlyList<string>? Concepts, IReadOnlyList<string>? Reasons, bool AllowsOperation);

public sealed record DimensionesDelCatalogoDeCarteraDto(
    IReadOnlyList<LineaDeCarteraDto> CreditLines,
    IReadOnlyList<ModalidadDeCarteraDto> Modalities,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<string> CollectorClasses,
    IReadOnlyList<string> RiskCategories,
    IReadOnlyList<string> Reasons);

// ============================================================================================================ catálogo --

public sealed record GetLendingRulesCatalogQuery : IRequest<Result<LendingRulesCatalogDto>>;

public sealed class GetLendingRulesCatalogQueryValidator : AbstractValidator<GetLendingRulesCatalogQuery>;

public sealed class GetLendingRulesCatalogQueryHandler(IDimensionesDeCartera dimensiones)
    : IRequestHandler<GetLendingRulesCatalogQuery, Result<LendingRulesCatalogDto>>
{
    public async Task<Result<LendingRulesCatalogDto>> Handle(GetLendingRulesCatalogQuery request, CancellationToken ct)
    {
        var catalogo = await dimensiones.CatalogoAsync(ct);
        var hechos = OperacionesDeCartera.Todas
            .Select(o => new HechoDelCatalogoDeCarteraDto(o.Codigo, o.Corto, o.EsInformativo, o.RolesDebito, o.RolesCredito, o.ModoPorDefecto,
                o.Comprobante, o.Entrega.ToString()))
            .ToList();
        var roles = R.Todos
            .Select(r => new RolDelCatalogoDeCarteraDto(r.Codigo, r.Naturaleza.ToString(), r.Familia.ToString(),
                r.Exigidas.Select(R.NombreDe).ToList(),
                r.Exigidas.Concat(r.Opcionales).Select(R.NombreDe).ToList(),
                r.Conceptos, r.Motivos, r.AdmiteHecho))
            .ToList();
        var motivos = R.Todos.SelectMany(r => r.Motivos ?? []).Distinct(StringComparer.Ordinal).ToList();
        return Result.Success(new LendingRulesCatalogDto(hechos, roles, new DimensionesDelCatalogoDeCarteraDto(
            catalogo.CreditLines, catalogo.Modalities, R.Conceptos, R.ClasesDeRecaudador, R.Categorias, motivos)));
    }
}

// ================================================================================================================ lista --

/// <summary>Los filtros de api.md §23.1 (<c>GET /rules?operation=&amp;role=&amp;asOf=…&amp;onlyCurrent=</c>). (nuevo)</summary>
public sealed record ListLendingPostingRulesQuery(
    string? Operation = null,
    string? Role = null,
    DateOnly? AsOf = null,
    string? CreditLineCode = null,
    string? ModalityCode = null,
    string? SubsegmentCode = null,
    string? RiskCategory = null,
    string? ConceptCode = null,
    string? CollectorClass = null,
    string? Account = null,
    bool OnlyCurrent = false,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<LendingPostingRuleDto>>>;

public sealed class ListLendingPostingRulesQueryValidator : AbstractValidator<ListLendingPostingRulesQuery>
{
    public ListLendingPostingRulesQueryValidator()
    {
        RuleFor(x => x.Operation).MaximumLength(40);
        RuleFor(x => x.Role).MaximumLength(40);
        RuleFor(x => x.Account).MaximumLength(30);
    }
}

public sealed class ListLendingPostingRulesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListLendingPostingRulesQuery, Result<PagedResult<LendingPostingRuleDto>>>
{
    public async Task<Result<PagedResult<LendingPostingRuleDto>>> Handle(ListLendingPostingRulesQuery request, CancellationToken ct)
    {
        var pagina = request.Pagina ?? new PageRequest();
        var consulta = db.LendingPostingRules.AsNoTracking().Where(r => !r.IsDeleted);

        if (Texto(request.Operation) is { } op) consulta = consulta.Where(r => r.Operation == op);
        if (Texto(request.Role) is { } rol) consulta = consulta.Where(r => r.Role == rol);
        if (Mayusculas(request.CreditLineCode) is { } l) consulta = consulta.Where(r => r.CreditLineCode == l);
        if (Mayusculas(request.ModalityCode) is { } m) consulta = consulta.Where(r => r.ModalityCode == m);
        if (Texto(request.SubsegmentCode) is { } s) consulta = consulta.Where(r => r.SubsegmentCode == s);
        if (LendingPostingRule.Categoria(request.RiskCategory) is { } k) consulta = consulta.Where(r => r.RiskCategory == k);
        if (Texto(request.ConceptCode) is { } c) consulta = consulta.Where(r => r.ConceptCode == c);
        if (Texto(request.CollectorClass) is { } q) consulta = consulta.Where(r => r.CollectorClass == q);
        if (Texto(request.Account) is { } cuenta)
            consulta = consulta.Where(r => db.ChartOfAccounts.Any(a => a.Id == r.AccountId && a.Code.StartsWith(cuenta)));

        var fecha = request.AsOf ?? (request.OnlyCurrent ? reloj.HoyLocal : (DateOnly?)null);
        if (fecha is { } f) consulta = consulta.Where(r => r.ValidFrom <= f && (r.ValidTo == null || r.ValidTo >= f));

        var total = await consulta.LongCountAsync(ct);
        var reglas = await consulta
            .OrderBy(r => r.Role).ThenBy(r => r.DimensionKey).ThenByDescending(r => r.ValidFrom)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);
        var items = await VistaDeReglasDeCartera.AsDtoAsync(db, reglas, reloj.HoyLocal, ct);
        return Result.Success(new PagedResult<LendingPostingRuleDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }

    private static string? Texto(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string? Mayusculas(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
}

// ======================================================================================================= detalle --

public sealed record GetLendingPostingRuleQuery(Guid RulePublicId) : IRequest<Result<LendingPostingRuleDto>>;

public sealed class GetLendingPostingRuleQueryValidator : AbstractValidator<GetLendingPostingRuleQuery>
{
    public GetLendingPostingRuleQueryValidator() => RuleFor(x => x.RulePublicId).NotEmpty();
}

public sealed class GetLendingPostingRuleQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetLendingPostingRuleQuery, Result<LendingPostingRuleDto>>
{
    public async Task<Result<LendingPostingRuleDto>> Handle(GetLendingPostingRuleQuery request, CancellationToken ct)
    {
        var regla = await db.LendingPostingRules.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (regla is null) return Result.Failure<LendingPostingRuleDto>(AccountingErrors.LendingRuleNotFound);
        return Result.Success((await VistaDeReglasDeCartera.AsDtoAsync(db, [regla], reloj.HoyLocal, ct))[0]);
    }
}

/// <summary>Las versiones de la misma <c>DimensionKey</c>, de la más nueva a la más vieja (<c>GET /rules/{id}/versions</c>). (nuevo)</summary>
public sealed record ListLendingPostingRuleVersionsQuery(Guid RulePublicId) : IRequest<Result<IReadOnlyList<LendingPostingRuleDto>>>;

public sealed class ListLendingPostingRuleVersionsQueryValidator : AbstractValidator<ListLendingPostingRuleVersionsQuery>
{
    public ListLendingPostingRuleVersionsQueryValidator() => RuleFor(x => x.RulePublicId).NotEmpty();
}

public sealed class ListLendingPostingRuleVersionsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListLendingPostingRuleVersionsQuery, Result<IReadOnlyList<LendingPostingRuleDto>>>
{
    public async Task<Result<IReadOnlyList<LendingPostingRuleDto>>> Handle(ListLendingPostingRuleVersionsQuery request, CancellationToken ct)
    {
        var clave = await db.LendingPostingRules.AsNoTracking().Where(r => r.PublicId == request.RulePublicId && !r.IsDeleted)
            .Select(r => r.DimensionKey).FirstOrDefaultAsync(ct);
        if (clave is null) return Result.Failure<IReadOnlyList<LendingPostingRuleDto>>(AccountingErrors.LendingRuleNotFound);
        var versiones = await db.LendingPostingRules.AsNoTracking().Where(r => r.DimensionKey == clave && !r.IsDeleted)
            .OrderByDescending(r => r.ValidFrom).ToListAsync(ct);
        return Result.Success(await VistaDeReglasDeCartera.AsDtoAsync(db, versiones, reloj.HoyLocal, ct));
    }
}

// ============================================================================================================ resolver --

/// <summary>La regla ganadora de una combinación, con su peso y las demás candidatas (§3.7). (nuevo)</summary>
public sealed record ResolucionDeReglaDeCarteraDto(
    LendingPostingRuleDto? Rule,
    CuentaDeReglaDeCarteraDto? Account,
    IReadOnlyList<CandidataDeReglaDeCarteraDto> Candidates,
    string? MissingCode,
    string? MissingMessage);

public sealed record CandidataDeReglaDeCarteraDto(Guid RulePublicId, string DimensionKey, int SpecificityWeight, bool Matched);

/// <summary>
/// <c>POST /api/accounting/lending/rules/resolve</c>: «¿a qué cuenta va esta combinación?» (§3.7). Recibe hecho, rol, dimensiones y fecha y
/// responde la regla ganadora con su peso y las candidatas del rol, con la misma <see cref="MatrizVigenteDeCartera.Resolver"/> que el
/// consumo. Sin regla, la respuesta lo dice (no es un error de la petición). (nuevo)
/// </summary>
public sealed record ResolveLendingRuleQuery(
    string Operation,
    string Role,
    DimensionesDeReglaDeCarteraDto Dimensions,
    DateOnly Date) : IRequest<Result<ResolucionDeReglaDeCarteraDto>>;

public sealed class ResolveLendingRuleQueryValidator : AbstractValidator<ResolveLendingRuleQuery>
{
    public ResolveLendingRuleQueryValidator()
    {
        RuleFor(x => x.Operation).NotEmpty().WithMessage("Indique el hecho.").MaximumLength(40);
        RuleFor(x => x.Role).NotEmpty().WithMessage("Indique el rol.")
            .Must(r => R.Buscar(r) is not null).WithMessage(x => $"«{x.Role}» no es un rol de la matriz de Cartera.");
        RuleFor(x => x.Dimensions).NotNull();
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Indique la fecha.");
    }
}

public sealed class ResolveLendingRuleQueryHandler(IApplicationDbContext db, ResolutorDeReglasDeCartera resolutor, IDateTimeService reloj)
    : IRequestHandler<ResolveLendingRuleQuery, Result<ResolucionDeReglaDeCarteraDto>>
{
    public async Task<Result<ResolucionDeReglaDeCarteraDto>> Handle(ResolveLendingRuleQuery request, CancellationToken ct)
    {
        var d = request.Dimensions ?? new DimensionesDeReglaDeCarteraDto();
        var matriz = await resolutor.CargarAsync(request.Date, request.Date, ct);
        var peticion = new PeticionDeReglaDeCartera(request.Operation.Trim(), request.Role.Trim(),
            new ValoresDeCartera(d.CreditLineCode, d.ModalityCode, d.SubsegmentCode, d.RiskCategory, d.ConceptCode, d.IsEmployee, d.InGracePeriod,
                d.OnBehalfOfAssociate, d.Restructured, d.CollectorClass, d.ReasonCode, d.BranchPublicId),
            request.Date, 1m, []);

        var candidatas = matriz.CandidatasDe(peticion)
            .Select(c => new CandidataDeReglaDeCarteraDto(c.Regla.PublicId, c.Regla.DimensionKey, c.Regla.SpecificityWeight, c.Coincide))
            .ToList();
        var resuelta = matriz.Resolver(peticion);
        if (resuelta.IsFailure)
            return Result.Success(new ResolucionDeReglaDeCarteraDto(null, null, candidatas, resuelta.Error.Code, resuelta.Error.Message));

        var regla = (await VistaDeReglasDeCartera.AsDtoAsync(db, [resuelta.Value!.Regla], reloj.HoyLocal, ct))[0];
        return Result.Success(new ResolucionDeReglaDeCarteraDto(regla, regla.Account, candidatas, null, null));
    }
}

/// <summary>De reglas a <see cref="LendingPostingRuleDto"/> con sus cuentas, sucursales y centros en bloque. (nuevo)</summary>
internal static class VistaDeReglasDeCartera
{
    public static async Task<IReadOnlyList<LendingPostingRuleDto>> AsDtoAsync(
        IApplicationDbContext db, IReadOnlyList<LendingPostingRule> reglas, DateOnly hoy, CancellationToken ct)
    {
        var idsCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var idsSucursal = reglas.Where(r => r.BranchId != null).Select(r => r.BranchId!.Value).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().Where(b => idsSucursal.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new ReferenciaDeReglaDeCarteraDto(b.PublicId, b.LegacyCode ?? b.Name), ct);
        var idsCentro = reglas.Where(r => r.CostCenterId != null).Select(r => r.CostCenterId!.Value).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking().Where(c => idsCentro.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new ReferenciaDeReglaDeCarteraDto(c.PublicId, c.LegacyCode ?? c.Name), ct);

        return reglas.Select(r =>
        {
            var cuenta = cuentas.GetValueOrDefault(r.AccountId);
            return new LendingPostingRuleDto(
                r.PublicId, r.Operation, r.Role,
                new DimensionesDeReglaDeCarteraGuardadaDto(r.CreditLineCode, r.ModalityCode, r.SubsegmentCode, r.RiskCategory, r.ConceptCode,
                    r.IsEmployee, r.InGracePeriod, r.OnBehalfOfAssociate, r.Restructured, r.CollectorClass, r.ReasonCode,
                    r.BranchId is { } b ? sucursales.GetValueOrDefault(b) : null,
                    r.CostCenterId is { } c ? centros.GetValueOrDefault(c) : null),
                new CuentaDeReglaDeCarteraDto(cuenta?.PublicId ?? Guid.Empty, cuenta?.Code ?? string.Empty, cuenta?.Name ?? string.Empty,
                    AccountEligibility.Reparo(cuenta, ModuloContable.Cartera) is null),
                r.ValidFrom, r.ValidTo, r.Notes, r.DimensionKey, r.SpecificityWeight, r.VigenteEn(hoy), r.CreatedBy);
        }).ToList();
    }
}
