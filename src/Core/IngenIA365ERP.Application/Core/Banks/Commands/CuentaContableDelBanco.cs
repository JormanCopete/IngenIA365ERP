using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Banks.Commands;

/// <summary>
/// La cuenta contable del banco (<c>COR_Banks.AccountingAccountCode</c>; feature 009 E4, T120 parte de bancos, decisión del dueño
/// del 2026-10-30): se conserva como texto y, si la contabilidad está iniciada y viene un código, debe ser una cuenta de movimiento,
/// activa y habilitada para Tesorería (<see cref="AccountEligibility.Verificar"/>). Sin contabilidad iniciada no se exige nada: el
/// banco es un maestro de Core que existe antes que el plan. (nuevo)
/// </summary>
internal static class CuentaContableDelBanco
{
    public static async Task<Result> ValidarAsync(IApplicationDbContext db, string? codigo, CancellationToken ct)
    {
        var limpio = codigo?.Trim();
        if (string.IsNullOrEmpty(limpio)) return Result.Success();
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct)) return Result.Success();
        var cuenta = await db.ChartOfAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == limpio && !a.IsDeleted, ct);
        var verificada = AccountEligibility.Verificar(cuenta, limpio, ModuloContable.Tesoreria);
        return verificada.IsFailure ? Result.Failure(verificada.Error) : Result.Success();
    }
}
