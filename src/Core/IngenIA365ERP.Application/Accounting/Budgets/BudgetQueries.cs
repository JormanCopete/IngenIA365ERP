using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Budgets;

/// <summary>
/// El presupuesto de un año: la versión pedida o, sin <c>Version</c>, la vigente (la de mayor
/// número que no esté <c>Superseded</c>). Un año sin presupuesto responde <c>Status = "None"</c>
/// con las listas vacías —éxito, no 404— para que la pantalla abra el año en blanco.
/// </summary>
public sealed record GetBudgetQuery(int Year, int? Version) : IRequest<Result<BudgetDto>>;

public sealed class GetBudgetQueryValidator : AbstractValidator<GetBudgetQuery>
{
    public GetBudgetQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(DateTime.UnixEpoch.Year, DateTime.MaxValue.Year);
        RuleFor(x => x.Version).GreaterThan(0).When(x => x.Version is not null);
    }
}

public sealed class GetBudgetQueryHandler(IApplicationDbContext db, IUserBranchScope alcance) : IRequestHandler<GetBudgetQuery, Result<BudgetDto>>
{
    public async Task<Result<BudgetDto>> Handle(GetBudgetQuery request, CancellationToken ct) =>
        await ArmadoDePresupuesto.ArmarAsync(db, request.Year, request.Version, await alcance.ObtenerAsync(ct), ct);
}

/// <summary>
/// Convierte el presupuesto guardado (una fila por cuenta, sucursal, centro y mes) en lo que la
/// pantalla pinta: una fila por (cuenta, sucursal, centro) con sus doce valores y el total, más la
/// lista de versiones. Lo usan la consulta y todos los comandos, que devuelven el presupuesto
/// como queda.
/// </summary>
public static class ArmadoDePresupuesto
{
    public const int Meses = 12;

    public static async Task<Result<BudgetDto>> ArmarAsync(IApplicationDbContext db, int year, int? version, AlcanceDeSucursales alcance, CancellationToken ct)
    {
        var ejercicioId = await db.FiscalYears.AsNoTracking().Where(f => f.Year == year && !f.IsDeleted).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);
        if (ejercicioId is null) return Result.Success(BudgetDto.Ninguno(year));

        var versiones = await db.Budgets.AsNoTracking()
            .Where(b => b.FiscalYearId == ejercicioId && !b.IsDeleted)
            .OrderBy(b => b.Version)
            .Select(b => new { b.Id, b.Version, b.Status, b.ApprovedAt, b.ApprovedBy, b.ChangeReason, b.CreatedAt, b.CreatedBy })
            .ToListAsync(ct);
        if (versiones.Count == 0) return Result.Success(BudgetDto.Ninguno(year));

        var elegida = version is { } v
            ? versiones.FirstOrDefault(x => x.Version == v)
            : versiones.Where(x => x.Status != BudgetStatus.Superseded).OrderByDescending(x => x.Version).FirstOrDefault();
        if (elegida is null) return Result.Failure<BudgetDto>(BudgetErrors.NotFound);

        var lineas = await LineasAsync(db, elegida.Id, alcance, ct);
        var dto = new BudgetDto(year, elegida.Version, elegida.Status.ToString(), elegida.ApprovedAt, elegida.ApprovedBy, elegida.ChangeReason,
            versiones.Select(x => new BudgetVersionDto(x.Version, x.Status.ToString(), x.ApprovedAt, x.ApprovedBy, x.ChangeReason, x.CreatedAt, x.CreatedBy)).ToList(),
            lineas);
        return Result.Success(dto);
    }

    /// <summary>Las filas de una versión, agrupadas por (cuenta, sucursal, centro) con los doce meses (cero donde no hay fila).</summary>
    public static async Task<IReadOnlyList<BudgetLineDto>> LineasAsync(IApplicationDbContext db, int budgetId, AlcanceDeSucursales alcance, CancellationToken ct)
    {
        var q = db.BudgetLines.AsNoTracking().Where(l => l.BudgetId == budgetId && !l.IsDeleted);
        // FR-035: con sucursales asignadas se ven las líneas de la empresa (sin sucursal) y las de esas
        // sucursales; las demás no salen. Hasta el 2026-09-20 el usuario de Norte veía lo presupuestado para Sur.
        if (alcance.Restringido)
        {
            var permitidas = alcance.Sucursales.ToList();
            q = q.Where(l => l.BranchId == null || permitidas.Contains(l.BranchId.Value));
        }
        var filas = await q
            .Select(l => new
            {
                l.AccountId, AccountPublicId = l.Account!.PublicId, AccountCode = l.Account!.Code, AccountName = l.Account!.Name,
                l.BranchId, BranchPublicId = l.Branch != null ? (Guid?)l.Branch.PublicId : null, BranchName = l.Branch != null ? l.Branch.Name : null,
                l.CostCenterId, CostCenterPublicId = l.CostCenter != null ? (Guid?)l.CostCenter.PublicId : null,
                CostCenterName = l.CostCenter != null ? (l.CostCenter.LegacyCode == null ? l.CostCenter.Name : l.CostCenter.LegacyCode + " " + l.CostCenter.Name) : null,
                l.Month, l.Amount,
            })
            .ToListAsync(ct);

        return filas
            .GroupBy(f => (f.AccountId, f.BranchId, f.CostCenterId))
            .Select(g =>
            {
                var primera = g.First();
                var montos = new decimal[Meses];
                foreach (var f in g.Where(f => f.Month is >= 1 and <= Meses)) montos[f.Month - 1] += f.Amount;
                return new BudgetLineDto(primera.AccountPublicId, primera.AccountCode, primera.AccountName, primera.BranchPublicId, primera.BranchName,
                    primera.CostCenterPublicId, primera.CostCenterName, montos, montos.Sum());
            })
            .OrderBy(l => l.AccountCode, StringComparer.Ordinal).ThenBy(l => l.BranchName).ThenBy(l => l.CostCenterName)
            .ToList();
    }

    /// <summary>La vigente con sus líneas vivas, las dos con seguimiento (los comandos las modifican).</summary>
    public sealed record Vigente(Budget Presupuesto, List<BudgetLine> Lineas);

    /// <summary>La vigente de un ejercicio: la de mayor versión que no esté <c>Superseded</c>; nula si el año no tiene presupuesto.</summary>
    public static async Task<Vigente?> VigenteAsync(IApplicationDbContext db, int fiscalYearId, CancellationToken ct)
    {
        var vigente = await db.Budgets
            .Where(b => b.FiscalYearId == fiscalYearId && !b.IsDeleted && b.Status != BudgetStatus.Superseded)
            .OrderByDescending(b => b.Version)
            .FirstOrDefaultAsync(ct);
        if (vigente is null) return null;
        // Las líneas se cargan aparte, no por la navegación: ésta traería también las retiradas.
        var lineas = await db.BudgetLines.Where(l => l.BudgetId == vigente.Id && !l.IsDeleted).ToListAsync(ct);
        return new Vigente(vigente, lineas);
    }
}
