using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Cartera;

// 2026-11-17 (pedido del dueño): lo común de las cifras de Cartera y captaciones del tablero de inicio. Las definiciones son las del
// tablero de Cartera (GetLendingDashboardQueryHandler) y de las vistas del centro de informes: los créditos vivos al corte y no castigados,
// del alcance de oficina y línea de quien pregunta (AmbitoDeInformeDeCartera), con la posición diaria vigente al corte —la proyección que
// escribe la reconstrucción del crédito—. La diferencia es sólo de rendimiento: la posición vigente se elige en la base (la última fecha y,
// en ella, la última versión), no trayendo a memoria toda la historia de posiciones de once mil créditos.

/// <summary>La posición vigente al corte de un crédito del alcance: su capital y sus días de mora.</summary>
internal sealed record PosicionDelTablero(int LoanId, decimal Capital, int Dias);

internal static class PosicionesDelTablero
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Las posiciones vigentes al <paramref name="corte"/> de los créditos vivos y no castigados del alcance de quien pregunta.</summary>
    public static async Task<IReadOnlyList<PosicionDelTablero>> AlCorteAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, DateOnly corte,
        CancellationToken ct)
    {
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcance, new FiltrosDeInformeDeCartera { AsOf = corte }, ct);
        var creditos = CargadorDeRiesgo.VivosAl(ambito.Creditos(db), corte).Where(l => l.Status != LoanStatus.WrittenOff).Select(l => l.Id);
        return await db.LoanDailyPositions.AsNoTracking()
            .Where(p => p.PositionDate <= corte && creditos.Contains(p.LoanId)
                        && !db.LoanDailyPositions.Any(q => q.LoanId == p.LoanId && q.PositionDate <= corte
                                                           && (q.PositionDate > p.PositionDate || (q.PositionDate == p.PositionDate && q.Version > p.Version))))
            .Select(p => new PosicionDelTablero(p.LoanId, p.Principal, p.DaysPastDue))
            .ToListAsync(ct);
    }

    /// <summary>El último día del mes anterior al de <paramref name="hoy"/>: el corte de la comparación.</summary>
    public static DateOnly FinDelMesAnterior(DateOnly hoy) => new DateOnly(hoy.Year, hoy.Month, 1).AddDays(-1);

    /// <summary>
    /// El saldo del libro de cuentas de una naturaleza (captación o aportes) del alcance al corte: la suma de los conceptos con fecha valor
    /// hasta el corte, como las vistas <c>deposit-balances</c> y <c>contributions-by-member</c>. Se suma en la base, sin traer las cuentas.
    /// </summary>
    public static async Task<decimal> SaldoDeCuentasAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, LineNature naturaleza,
        IReadOnlyCollection<MemberAccountConcept> conceptos, DateOnly corte, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().Where(a => a.Nature == naturaleza && !a.IsDeleted)
            .PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId).Select(a => a.Id);
        var deLosConceptos = conceptos.ToArray();
        return await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => cuentas.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte && deLosConceptos.Contains(t.Concept))
            .SumAsync(t => t.Amount, ct);
    }

    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);
}
