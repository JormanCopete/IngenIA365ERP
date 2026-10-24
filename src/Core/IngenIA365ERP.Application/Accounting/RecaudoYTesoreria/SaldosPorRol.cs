using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>
/// El saldo contable de un rol de la matriz a una fecha (feature 015, T0259; FR-146, FR-209): la suma de <c>ACC_JournalEntries</c> contabilizados (no hay saldos
/// guardados, FR-046 de la 009) sobre las cuentas a las que la matriz vigente lleva ese rol —o la cuenta vinculada a la cuenta propia, en los roles por vínculo—,
/// de la sucursal de la oficina si se pide. Sin regla para el rol no hay dato: la respuesta vacía dice «sin dato», nunca un cero inventado. (nuevo)
/// </summary>
public static class SaldosPorRol
{
    public static async Task<IReadOnlyList<SaldoContableDto>> SaldoAsync(IApplicationDbContext db, ResolutorDeLaMatriz resolutor, ModuloDeLaMatriz modulo,
        SolicitudDeSaldo solicitud, CancellationToken ct)
    {
        var rol = modulo.Rol(solicitud.Concept);
        if (rol is null) return [];
        List<int> cuentas;
        if (rol.PorVinculo)
        {
            if (solicitud.OwnBankAccountPublicId is not { } propia) return [];
            cuentas = await db.OwnBankAccountLinks.AsNoTracking()
                .Where(l => l.OwnBankAccountPublicId == propia && l.ValidFrom <= solicitud.Date && (l.ValidTo == null || l.ValidTo >= solicitud.Date))
                .Select(l => l.ChartOfAccountId).ToListAsync(ct);
        }
        else
        {
            var matriz = await resolutor.CargarAsync(modulo, solicitud.Date, solicitud.Date, ct);
            cuentas = matriz.Reglas.Where(r => r.Role == rol.Codigo && r.VigenteEn(solicitud.Date)).Select(r => r.AccountId).Distinct().ToList();
        }
        if (cuentas.Count == 0) return [];

        var consulta = db.JournalEntries.AsNoTracking().Where(j => cuentas.Contains(j.AccountId) && j.IsPosted && j.Date <= solicitud.Date);
        if (solicitud.OfficePublicId is { } oficina)
        {
            var sucursal = await db.Branches.AsNoTracking().Where(b => b.PublicId == oficina).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
            if (sucursal is null) return [];
            consulta = consulta.Where(j => j.BranchId == sucursal.Value);
        }
        var saldo = await consulta.SumAsync(j => (decimal?)(j.Debit - j.Credit), ct) ?? 0m;
        return [new SaldoContableDto(solicitud.Concept, solicitud.OfficePublicId, solicitud.OwnBankAccountPublicId, saldo)];
    }
}
