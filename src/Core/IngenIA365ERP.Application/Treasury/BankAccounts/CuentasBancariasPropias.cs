using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.BankAccounts;

/// <summary>
/// La implementación del puerto <see cref="ICuentasBancariasPropias"/> (feature 015, US10, T0138; R-05; FR-102, FR-175; contracts/interfaces.md §2): lo que
/// Recaudo ve de las cuentas propias —las de recaudo vigentes, con el alcance por cuenta de <c>TES_UserBankAccountScopes</c>, que es también el de los canales
/// sin caja (FR-175)— sin nombrar una tabla de Tesorería. El número sale enmascarado: Recaudo guarda el <c>PublicId</c> y muestra el código. Sólo lee. (nuevo)
/// </summary>
public sealed class CuentasBancariasPropias(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj) : ICuentasBancariasPropias
{
    public async Task<IReadOnlyList<CuentaBancariaPropiaDto>> DeRecaudoAsync(Guid? branchPublicId, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var mio = await alcance.ObtenerAsync(ct);
        int? oficina = branchPublicId is { } b
            ? await db.Branches.AsNoTracking().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        if (branchPublicId is not null && oficina is null) return [];
        var cuentas = await db.OwnBankAccounts.AsNoTracking()
            .Where(c => c.Status == TreasuryCatalogStatus.Active && (oficina == null || c.BranchId == oficina))
            .OrderBy(c => c.Code)
            .ToListAsync(ct);
        var lista = new List<CuentaBancariaPropiaDto>();
        foreach (var c in cuentas.Where(c => c.Purposes.HasFlag(BankAccountPurpose.Collection) && c.Activa(hoy) && mio.IncluyeCuenta(c.Id)))
            lista.Add(await DtoAsync(c, hoy, ct));
        return lista;
    }

    public async Task<CuentaBancariaPropiaDto?> PorPublicIdAsync(Guid publicId, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == publicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id)) return null;
        return await DtoAsync(cuenta, reloj.HoyLocal, ct);
    }

    /// <summary>El número de la cuenta con sólo sus cuatro últimos dígitos.</summary>
    public static string Enmascarar(string numero) =>
        string.IsNullOrEmpty(numero) || numero.Length <= 4 ? numero : new string('*', numero.Length - 4) + numero[^4..];

    private async Task<CuentaBancariaPropiaDto> DtoAsync(OwnBankAccount c, DateOnly hoy, CancellationToken ct)
    {
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == c.BankId).Select(b => new { b.Name, b.TransferCode }).FirstAsync(ct);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == c.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        return new CuentaBancariaPropiaDto(c.PublicId, c.Code, banco.Name, banco.TransferCode, c.AccountType.ToString(), Enmascarar(c.AccountNumber), oficina,
            c.Purposes.HasFlag(BankAccountPurpose.Collection), c.Activa(hoy));
    }
}
