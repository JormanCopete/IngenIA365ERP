using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Accounts;

/// <summary>
/// Qué cuenta puede parametrizar otro módulo (feature 009, FR-016): de movimiento, activa y
/// habilitada para ese módulo. Lo consultan los comandos que guardan una parametrización
/// (cuentas por concepto de nómina, líneas de crédito, conceptos de tesorería, productos…) y
/// los contabilizadores de módulo al resolver un código en una cuenta. Con la contabilidad sin
/// iniciar responde <c>Accounting.NotInitialized</c>; con una cuenta que no cumple,
/// <c>Accounting.Account.NotEligible</c> con <c>accountCode</c>, <c>module</c> y <c>rule</c>.
/// </summary>
public sealed class AccountEligibility(IApplicationDbContext db)
{
    public async Task<Result<ChartOfAccount>> ResolverPorCodigoAsync(string? code, string module, CancellationToken ct)
    {
        if (!await IniciadaAsync(ct)) return Result.Failure<ChartOfAccount>(AccountingErrors.NotInitialized);
        var codigo = code?.Trim() ?? string.Empty;
        if (codigo.Length == 0) return Result.Failure<ChartOfAccount>(AccountingErrors.AccountNotFound("(sin código)"));
        var cuenta = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.Code == codigo && !a.IsDeleted, ct);
        return Verificar(cuenta, codigo, module);
    }

    public async Task<Result<ChartOfAccount>> ResolverPorIdAsync(int id, string module, CancellationToken ct)
    {
        if (!await IniciadaAsync(ct)) return Result.Failure<ChartOfAccount>(AccountingErrors.NotInitialized);
        var cuenta = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
        return Verificar(cuenta, id.ToString(), module);
    }

    public async Task<Result<ChartOfAccount>> ResolverPorPublicIdAsync(Guid publicId, string module, CancellationToken ct)
    {
        if (!await IniciadaAsync(ct)) return Result.Failure<ChartOfAccount>(AccountingErrors.NotInitialized);
        var cuenta = await db.ChartOfAccounts.FirstOrDefaultAsync(a => a.PublicId == publicId && !a.IsDeleted, ct);
        return Verificar(cuenta, publicId.ToString(), module);
    }

    /// <summary>La regla pura, para quien ya tiene la cuenta en la mano.</summary>
    public static Result<ChartOfAccount> Verificar(ChartOfAccount? cuenta, string referencia, string module)
    {
        if (cuenta is null) return Result.Failure<ChartOfAccount>(AccountingErrors.AccountNotFound(referencia));
        if (!cuenta.IsMovement) return Result.Failure<ChartOfAccount>(AccountingErrors.AccountNotEligible(cuenta.Code, module, "no es de movimiento"));
        if (!cuenta.IsActive) return Result.Failure<ChartOfAccount>(AccountingErrors.AccountNotEligible(cuenta.Code, module, "está inactiva"));
        if (!ModuloContable.Habilitada(cuenta.EnabledModules, module))
            return Result.Failure<ChartOfAccount>(AccountingErrors.AccountNotEligible(cuenta.Code, module, $"no está habilitada para {ModuloContable.Nombre(module)}"));
        return Result.Success(cuenta);
    }

    /// <summary>Por qué no sirve, en una frase, o null si sirve; para listar parametrizaciones inválidas sin cortar en la primera.</summary>
    public static string? Reparo(ChartOfAccount? cuenta, string module) =>
        cuenta is null ? "la cuenta no existe"
        : !cuenta.IsMovement ? "no es de movimiento"
        : !cuenta.IsActive ? "está inactiva"
        : !ModuloContable.Habilitada(cuenta.EnabledModules, module) ? $"no está habilitada para {ModuloContable.Nombre(module)}"
        : null;

    private Task<bool> IniciadaAsync(CancellationToken ct) => db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct);
}
