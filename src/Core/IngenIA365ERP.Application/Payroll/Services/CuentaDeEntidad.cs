using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Concepts.Queries;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// La cuenta contable propia de una entidad de seguridad social (EPS, fondo de pensiones, ARL, fondo de
/// cesantías, caja; columna <c>PayrollAccountId</c> de su catálogo). Cuando la tiene, el comprobante de
/// nómina acredita esa cuenta en vez de la cuenta crédito del concepto (<see cref="PayrollAccountingPoster"/>).
/// Las cinco pantallas la escriben por aquí, con las mismas reglas que las cuentas de un concepto:
/// auxiliar (de movimiento), activa y habilitada para Nómina; y si exige tercero, la entidad tiene que
/// tener su persona vinculada.
/// </summary>
public static class CuentaDeEntidad
{
    /// <summary>Código vacío = sin cuenta propia (null). Si no, el Id de una cuenta que cumple las reglas.</summary>
    public static async Task<Result<int?>> ResolverAsync(IApplicationDbContext db, string? accountCode, int? personId, string entidad, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountCode)) return Result.Success<int?>(null);
        var code = accountCode.Trim();
        var cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code && !a.IsDeleted, ct);
        if (cuenta is null)
            return Result.Failure<int?>(new Error("Payroll.AccountNotFound", $"Cuentas contables inexistentes: {code}."));
        if (AccountEligibility.Reparo(cuenta, ModuloContable.Nomina) is { } reparo)
            return Result.Failure<int?>(AccountingErrors.AccountNotEligible(cuenta.Code, ModuloContable.Nomina, reparo));
        if (cuenta.RequiresThirdParty && personId is null)
            return Result.Failure<int?>(new Error(AccountingErrors.InstitutionalLinkMissing(entidad).Code,
                $"La cuenta {cuenta.Code} exige tercero y {entidad} no tiene persona vinculada. Elija la persona que la representa antes de asignarle la cuenta."));
        return Result.Success<int?>(cuenta.Id);
    }

    /// <summary>Las cuentas con sus reglas y el reparo si ya no sirven, para mostrarlas en las pantallas de los catálogos.</summary>
    public static async Task<IReadOnlyDictionary<int, CuentaParametrizadaDto>> DescribirAsync(IApplicationDbContext db, IEnumerable<int?> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (lista.Count == 0) return new Dictionary<int, CuentaParametrizadaDto>();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().IgnoreQueryFilters().Where(c => lista.Contains(c.Id)).ToListAsync(ct);
        return cuentas.ToDictionary(c => c.Id, c => new CuentaParametrizadaDto(c.PublicId, c.Code, c.Name, c.IsMovement, c.IsActive,
            c.RequiresThirdParty, c.RequiresCrossDocument, c.RequiresCostCenter, c.RequiresBranch,
            c.IsDeleted ? "la cuenta fue eliminada" : AccountEligibility.Reparo(c, ModuloContable.Nomina)));
    }
}
