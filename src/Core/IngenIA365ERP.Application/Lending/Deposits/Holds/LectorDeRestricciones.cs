using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>
/// Lo que restan los bloqueos vigentes de una cuenta a una fecha (FR-057): lo bloqueado por motivos propios, lo embargado y si hay un bloqueo total. Un
/// bloqueo total cuenta como todo el saldo bloqueado. (nuevo)
/// </summary>
public sealed record RestriccionesDeCuenta(decimal Bloqueado, decimal Embargado, bool BloqueoTotal, IReadOnlyList<HoldReason> Motivos)
{
    public static RestriccionesDeCuenta Ninguna { get; } = new(0m, 0m, false, []);
}

/// <summary>
/// El único lector de los bloqueos de las cuentas (feature 014, US11, T189; data-model §5.1; FR-057): los vigentes a una fecha —sin levantar y dentro de sus
/// fechas—, también los agregados en la unidad de trabajo sin guardar (el registro del embargo y la consignación los leen antes de guardar). El disponible de
/// <c>ReglasDeMovimientos</c>, la cancelación y el retiro lo leen de aquí. (nuevo)
/// </summary>
public static class LectorDeRestricciones
{
    /// <summary>Los bloqueos vigentes de las cuentas a <paramref name="fecha"/>, por cuenta.</summary>
    public static async Task<Dictionary<int, List<AccountHold>>> VigentesAsync(IApplicationDbContext db, IReadOnlyCollection<int> cuentas, DateOnly fecha,
        CancellationToken ct)
    {
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var guardados = await db.AccountHolds.AsNoTracking()
            .Where(h => ids.Contains(h.MemberAccountId) && h.ReleasedAt == null && !h.IsDeleted && h.ValidFrom <= fecha && (h.ValidTo == null || h.ValidTo >= fecha))
            .ToListAsync(ct);
        // Los que la unidad de trabajo ya cambió (levantados o nuevos) mandan sobre lo guardado.
        var locales = db.AccountHolds.Local.Where(h => ids.Contains(h.MemberAccountId)).ToList();
        var porId = locales.Where(h => h.Id != 0).ToDictionary(h => h.Id);
        var todos = guardados.Select(g => porId.GetValueOrDefault(g.Id) ?? g).Concat(locales.Where(h => h.Id == 0)).Where(h => h.VigenteEn(fecha));
        return todos.GroupBy(h => h.MemberAccountId).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Lo bloqueado y lo embargado de cada cuenta con su saldo del titular (el total bloquea todo el saldo).</summary>
    public static async Task<Dictionary<int, RestriccionesDeCuenta>> RestriccionesAsync(IApplicationDbContext db, IReadOnlyDictionary<int, decimal> saldos,
        DateOnly fecha, CancellationToken ct)
    {
        var vigentes = await VigentesAsync(db, saldos.Keys.ToList(), fecha, ct);
        var r = new Dictionary<int, RestriccionesDeCuenta>();
        foreach (var (cuenta, bloqueos) in vigentes)
        {
            var saldo = Math.Max(0m, saldos.GetValueOrDefault(cuenta));
            var total = bloqueos.Any(b => b.Reason != HoldReason.Garnishment && b.EsTotal);
            var embargado = bloqueos.Where(b => b.Reason == HoldReason.Garnishment).Sum(b => b.Amount ?? saldo);
            var bloqueado = total ? saldo : bloqueos.Where(b => b.Reason != HoldReason.Garnishment).Sum(b => b.Amount ?? 0m);
            r[cuenta] = new RestriccionesDeCuenta(bloqueado, embargado, total, bloqueos.Select(b => b.Reason).Distinct().ToList());
        }
        return r;
    }
}
