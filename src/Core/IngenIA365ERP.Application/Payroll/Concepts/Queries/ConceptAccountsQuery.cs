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
