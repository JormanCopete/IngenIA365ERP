using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Concepts.Queries;

/// <summary>Una cuenta parametrizada, con sus reglas para que la pantalla las muestre (feature 009, T062) y el reparo si ya no sirve.</summary>
public sealed record CuentaParametrizadaDto(Guid PublicId, string Code, string Name, bool IsMovement, bool IsActive, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresCostCenter, bool RequiresBranch, string? Problem);

public sealed record CuentaDeConceptoDto(Guid? CostCenterPublicId, string? CostCenterName, CuentaParametrizadaDto Debit, CuentaParametrizadaDto Credit);

/// <summary><c>GET /api/payroll/concept-definitions/{code}/accounts</c>: las filas vigentes (por defecto y por centro de costo).</summary>
public sealed record GetConceptAccountsQuery(string Code) : IRequest<Result<IReadOnlyList<CuentaDeConceptoDto>>>;

public sealed class GetConceptAccountsQueryValidator : AbstractValidator<GetConceptAccountsQuery>
{
    public GetConceptAccountsQueryValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
}

public sealed class GetConceptAccountsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetConceptAccountsQuery, Result<IReadOnlyList<CuentaDeConceptoDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaDeConceptoDto>>> Handle(GetConceptAccountsQuery request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var filas = await db.PayrollConceptDefinitionAccounts.AsNoTracking()
            .Where(a => a.ConceptCode == code && !a.IsDeleted)
            .Select(a => new { a.CostCenterId, a.DebitAccountId, a.CreditAccountId })
            .ToListAsync(ct);
        if (filas.Count == 0) return Result.Success<IReadOnlyList<CuentaDeConceptoDto>>([]);

        var ids = filas.SelectMany(f => new[] { f.DebitAccountId, f.CreditAccountId }).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var ccIds = filas.Where(f => f.CostCenterId != null).Select(f => f.CostCenterId!.Value).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking().Where(c => ccIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => new { c.PublicId, c.Name }, ct);

        CuentaParametrizadaDto Cuenta(int id)
        {
            var c = cuentas.GetValueOrDefault(id);
            if (c is null) return new(Guid.Empty, id.ToString(), "(cuenta inexistente)", false, false, false, false, false, false, "la cuenta no existe");
            var reparo = c.IsDeleted ? "la cuenta fue eliminada" : AccountEligibility.Reparo(c, ModuloContable.Nomina);
            return new(c.PublicId, c.Code, c.Name, c.IsMovement, c.IsActive, c.RequiresThirdParty, c.RequiresCrossDocument, c.RequiresCostCenter, c.RequiresBranch, reparo);
        }

        var lista = filas.OrderBy(f => f.CostCenterId.HasValue).Select(f =>
        {
            var cc = f.CostCenterId is { } id ? centros.GetValueOrDefault(id) : null;
            return new CuentaDeConceptoDto(cc?.PublicId, cc?.Name, Cuenta(f.DebitAccountId), Cuenta(f.CreditAccountId));
        }).ToList();
        return Result.Success<IReadOnlyList<CuentaDeConceptoDto>>(lista);
    }
}

/// <summary>Una entidad del catálogo con cuenta propia para la nómina.</summary>
public sealed record EntidadConCuentaDto(string Name, string AccountCode, string AccountName);

/// <summary>
/// Si la cuenta crédito del concepto se reemplaza por la cuenta propia de cada entidad (EPS, fondo, ARL, caja) al
/// contabilizar: de qué catálogo (<c>EntityKind</c> nulo = ninguno), en qué alcance, cuáles entidades la tienen y
/// cuántas no (ésas siguen con la cuenta crédito del concepto).
/// </summary>
public sealed record CreditoPorEntidadDto(string? EntityKind, string? EntityName, string? Screen, string? Scope,
    IReadOnlyList<EntidadConCuentaDto> WithAccount, int WithoutAccount);

/// <summary><c>GET /api/payroll/concept-definitions/{code}/entity-accounts</c>: el aviso del diálogo de cuentas del concepto.</summary>
public sealed record GetConceptEntityAccountsQuery(string Code) : IRequest<Result<CreditoPorEntidadDto>>;

public sealed class GetConceptEntityAccountsQueryValidator : AbstractValidator<GetConceptEntityAccountsQuery>
{
    public GetConceptEntityAccountsQueryValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
}

public sealed class GetConceptEntityAccountsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetConceptEntityAccountsQuery, Result<CreditoPorEntidadDto>>
{
    public async Task<Result<CreditoPorEntidadDto>> Handle(GetConceptEntityAccountsQuery request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var concepto = await db.PayrollConceptDefinitions.AsNoTracking().Where(c => c.Code == code).Select(c => new { c.Nature }).FirstOrDefaultAsync(ct);
        if (concepto is null)
            return Result.Failure<CreditoPorEntidadDto>(new Error("Payroll.ConceptNotFound", $"No existe el concepto {code}."));

        var entidad = Services.TercerosDeNomina.EntidadDeLaCuenta(code, concepto.Nature, Services.TercerosDeNomina.EntidadDe(code, concepto.Nature));
        string? alcance = null;
        // En las cesantías anuales la cuenta por pagar de CESANTIAS es del fondo (SettlementAccountingPoster.TerceroPara).
        if (entidad == Services.EntidadInstitucional.Ninguna && code == Domain.Payroll.Calculation.WellKnownConceptCodes.Severance)
        {
            entidad = Services.EntidadInstitucional.FondoDeCesantias;
            alcance = "Sólo en la liquidación de cesantías anuales; en la definitiva el crédito es del empleado con la cuenta del concepto.";
        }
        if (entidad == Services.EntidadInstitucional.Ninguna)
            return Result.Success(new CreditoPorEntidadDto(null, null, null, null, [], 0));

        var filas = entidad switch
        {
            Services.EntidadInstitucional.Eps => await db.HealthInsuranceProviders.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Name, x.PayrollAccountId }).ToListAsync(ct),
            Services.EntidadInstitucional.FondoDePensiones => await db.PensionProviders.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Name, x.PayrollAccountId }).ToListAsync(ct),
            Services.EntidadInstitucional.Arl => await db.WorkRiskProviders.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Name, x.PayrollAccountId }).ToListAsync(ct),
            Services.EntidadInstitucional.FondoDeCesantias => await db.SeveranceProviders.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Name, x.PayrollAccountId }).ToListAsync(ct),
            _ => await db.FamilyCompensationFunds.AsNoTracking().Where(x => !x.IsDeleted).Select(x => new { x.Name, x.PayrollAccountId }).ToListAsync(ct),
        };
        var cuentas = await Services.CuentaDeEntidad.DescribirAsync(db, filas.Select(f => f.PayrollAccountId), ct);
        var con = filas.Where(f => f.PayrollAccountId is { } id && cuentas.ContainsKey(id))
            .Select(f => { var c = cuentas[f.PayrollAccountId!.Value]; return new EntidadConCuentaDto(f.Name, c.Code, c.Name); })
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return Result.Success(new CreditoPorEntidadDto(entidad.ToString(), Services.TercerosDeNomina.Nombre(entidad),
            Services.TercerosDeNomina.Pantalla(entidad), alcance, con, filas.Count - con.Count));
    }
}
