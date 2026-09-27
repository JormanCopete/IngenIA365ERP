using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Services;

/// <summary>
/// Dónde está parametrizada una cuenta (feature 009, FR-011, data-model.md §1): por FK en nómina,
/// tesorería, la configuración y la matriz de Inventario (feature 012); por código en cartera, ahorros, CDT y bancos, que
/// todavía guardan la cuenta como texto (E3 los pasa a FK). Vive en Persistence porque recorre
/// tablas de siete módulos y el contexto de pruebas de Application no las materializa todas.
/// </summary>
internal sealed class AccountReferenceFinder(ApplicationDbContext db) : IAccountReferenceFinder
{
    public async Task<IReadOnlyList<ReferenciaDeCuenta>> BuscarAsync(int accountId, string code, CancellationToken ct)
    {
        var lista = new List<ReferenciaDeCuenta>();

        foreach (var c in await db.PayrollConceptDefinitionAccounts.AsNoTracking().Where(a => !a.IsDeleted && (a.DebitAccountId == accountId || a.CreditAccountId == accountId)).Select(a => a.ConceptCode).Distinct().ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Nomina), "Cuentas por concepto", c));

        if (await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted && s.ResultAccountId == accountId, ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Contabilidad), "Configuración", "Cuenta de resultado del ejercicio"));

        foreach (var n in await db.TreasuryConcepts.AsNoTracking().Where(t => !t.IsDeleted && (t.DebitAccountId == accountId || t.CreditAccountId == accountId)).Select(t => t.Name).ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Tesoreria), "Conceptos de tesorería", n));

        foreach (var n in await db.CreditLineParameters.AsNoTracking().Where(l => !l.IsDeleted && (l.AccountCode == code || l.AccountInterestIncome == code || l.AccountInterestCxC == code ||
                     l.AccountInterestDefault == code || l.AccountInterestAdvance == code || l.ProvisionExpenseAccountCode == code || l.ProvisionAccountCode == code || l.VatAccount == code))
                     .Select(l => l.Description).ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Cartera), "Líneas de crédito", n));

        foreach (var n in await db.SavingsParameters.AsNoTracking().Where(s => !s.IsDeleted && (s.TreasuryAccount == code || s.InterestExpenseAccount == code)).Select(s => s.Id.ToString()).ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Cdt), "Parámetros de ahorro", n));

        foreach (var n in await db.CdtParameters.AsNoTracking().Where(s => !s.IsDeleted && (s.TreasuryAccount == code || s.InterestExpenseAccount == code)).Select(s => s.Id.ToString()).ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Cdt), "Parámetros de CDT", n));

        // Feature 012 (T494, G7): la matriz de Inventario, por FK. Cuenta toda versión viva, también las cerradas: la FK es
        // Restrict y el comprobante de un mensaje viejo se resuelve con la regla vigente a su fecha.
        foreach (var r in await db.InventoryPostingRules.AsNoTracking().Where(r => !r.IsDeleted && r.AccountId == accountId)
                     .OrderBy(r => r.Operation).ThenBy(r => r.Role).ThenBy(r => r.ValidFrom)
                     .Select(r => new { r.Operation, r.Role, r.AccountingGroupCode, r.WarehouseCode, r.ValidFrom, r.ValidTo }).ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Inventario), "Matriz de contabilización",
                $"{r.Operation} · {r.Role}{(r.AccountingGroupCode is null ? "" : $" · grupo {r.AccountingGroupCode}")}{(r.WarehouseCode is null ? "" : $" · bodega {r.WarehouseCode}")} · desde {r.ValidFrom:yyyy-MM-dd}{(r.ValidTo is { } h ? $" hasta {h:yyyy-MM-dd}" : "")}"));

        foreach (var n in await db.Banks.AsNoTracking().Where(b => !b.IsDeleted && b.AccountingAccountCode == code).Select(b => b.Name).ToListAsync(ct))
            lista.Add(new(ModuloContable.Nombre(ModuloContable.Tesoreria), "Bancos", n));

        return lista;
    }
}
