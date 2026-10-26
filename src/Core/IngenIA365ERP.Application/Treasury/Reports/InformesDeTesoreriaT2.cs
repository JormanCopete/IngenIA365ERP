using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Liquidity;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reports;

// Feature 015, T2 (fase 45, T0465; FR-153 parte T2; api.md §17): las vistas de T2 del centro de informes de Tesorería —las conciliaciones (mínimas y completas) con sus
// cruces automáticos y manuales, las fotos de la posición de liquidez y el flujo de caja proyectado contra lo real—. Sólo leen; las conciliaciones con el alcance por cuenta
// propia y la liquidez y el flujo, que son de toda la cooperativa, sólo para quien ve todas las cuentas.
public sealed partial class TreasuryReportQueryHandler
{
    private async Task<TablaExportable> ConciliacionesT2Async(Func<int, bool> ve, Dictionary<int, OwnBankAccount> cuentas, DateOnly desde, DateOnly hasta,
        CancellationToken ct)
    {
        var de = desde.Year * 100 + desde.Month;
        var a = hasta.Year * 100 + hasta.Month;
        var conciliaciones = (await db.TreasuryReconciliations.AsNoTracking()
                .Where(r => r.PeriodYear * 100 + r.PeriodMonth >= de && r.PeriodYear * 100 + r.PeriodMonth <= a)
                .OrderBy(r => r.BankAccountId).ThenBy(r => r.PeriodYear).ThenBy(r => r.PeriodMonth).ThenBy(r => r.Version).ToListAsync(ct))
            .Where(r => ve(r.BankAccountId)).ToList();
        var ids = conciliaciones.Select(r => r.Id).ToList();
        var cruces = await db.TreasuryReconciliationMatches.AsNoTracking().Where(m => ids.Contains(m.ReconciliationId) && m.IsLive)
            .Select(m => new { m.ReconciliationId, m.Mode, m.RecordKind }).ToListAsync(ct);
        var partidas = await db.TreasuryReconcilingItems.AsNoTracking().Where(i => ids.Contains(i.ReconciliationId) && !i.IsVoided)
            .Select(i => new { i.ReconciliationId, i.Amount }).ToListAsync(ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Cuenta"), new("Período"), new("Clase"), new("Versión", TipoDeColumna.Entero), new("Estado"), new("Saldo del extracto", TipoDeColumna.Moneda),
            new("Saldo en libros", TipoDeColumna.Moneda), new("Cruces automáticos", TipoDeColumna.Entero), new("Cruces manuales", TipoDeColumna.Entero),
            new("Notas bancarias cruzadas", TipoDeColumna.Entero), new("Liquidaciones de redes cruzadas", TipoDeColumna.Entero), new("Partidas", TipoDeColumna.Moneda),
            new("Diferencia", TipoDeColumna.Moneda), new("Cerrada"), new("_conciliacion", Clave: "_conciliacion"),
        };
        var filas = conciliaciones.Select(r =>
        {
            var suyos = cruces.Where(c => c.ReconciliationId == r.Id).ToList();
            return new FilaExportable(
            [
                Cuenta(cuentas, r.BankAccountId), $"{r.PeriodYear}-{r.PeriodMonth:00}", r.Kind == ReconciliationKind.Complete ? "Completa" : "Mínima", r.Version, r.Status.ToString(),
                r.StatementBalance, r.LedgerBalance, suyos.Count(c => c.Mode == ReconciliationMatchMode.Automatic), suyos.Count(c => c.Mode == ReconciliationMatchMode.Manual),
                suyos.Count(c => c.RecordKind == ReconciliationRecordKind.BankNote), suyos.Count(c => c.RecordKind == ReconciliationRecordKind.AcquirerSettlement),
                partidas.Where(p => p.ReconciliationId == r.Id).Sum(p => p.Amount), r.Difference, r.ClosedAt?.ToString("yyyy-MM-dd"), r.PublicId,
            ], Resaltada: r.Difference != 0m || r.Status == ReconciliationState.Outdated);
        }).ToList();
        return new TablaExportable("Conciliaciones bancarias", Periodo(desde, hasta), columnas, filas, null,
            ["Desde T2 la conciliación nueva es completa: cruza con las reglas de la cuenta, las notas bancarias aprobadas, las dispersiones de Nómina y las liquidaciones de las "
             + "redes de tarjetas (FR-128 a FR-133)."]);
    }

    private async Task<TablaExportable> LiquidezAsync(AlcanceDeTesoreria mio, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var columnas = new List<ColumnaExportable>
        {
            new("Foto"), new("Día", TipoDeColumna.Fecha), new("Bancos", TipoDeColumna.Moneda), new("Bóvedas", TipoDeColumna.Moneda), new("Cajas", TipoDeColumna.Moneda),
            new("Compromisos", TipoDeColumna.Moneda), new("Esperado de Cartera", TipoDeColumna.Moneda), new("Esperado de Captaciones", TipoDeColumna.Moneda),
            new("Disponible", TipoDeColumna.Moneda), new("Proyectado", TipoDeColumna.Moneda), new("Fondo de liquidez", TipoDeColumna.Moneda),
            new("Componentes que faltaron", TipoDeColumna.Entero), new("Informado a Captaciones"), new("_foto", Clave: "_foto"),
        };
        if (!mio.Total)
            return new TablaExportable("Posición de liquidez", Periodo(desde, hasta), columnas, [], null,
                ["La posición es de toda la cooperativa: la ve quien tiene todas las cuentas a su alcance (Treasury.Scope.All)."]);
        var fotos = await db.TreasuryLiquiditySnapshots.AsNoTracking().Where(f => f.Date >= desde && f.Date <= hasta).OrderBy(f => f.Date).ThenBy(f => f.TakenAt).ToListAsync(ct);
        var filas = fotos.Select(f =>
        {
            var dto = LiquidezDeTesoreria.Dto(f);
            return new FilaExportable(
            [
                f.Number, f.Date, f.BanksTotal, f.VaultsTotal, f.CashSessionsTotal, f.CommitmentsTotal, f.ExpectedLending, f.ExpectedDeposits, f.Available, f.Projected,
                f.LiquidityFundTotal, dto.MissingCount, f.FundMessagePublicId is null ? "No" : "Sí", f.PublicId,
            ], Resaltada: dto.MissingCount > 0);
        }).ToList();
        return new TablaExportable("Posición de liquidez", Periodo(desde, hasta), columnas, filas, null,
            ["Cada fila es una foto tomada en la pantalla de liquidez. Un componente que no respondió no cuenta en la posición de esa foto (FR-137); la fila se resalta."]);
    }

    private async Task<TablaExportable> FlujoDeCajaAsync(AlcanceDeTesoreria mio, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var columnas = new List<ColumnaExportable>
        {
            new("Desde", TipoDeColumna.Fecha), new("Hasta", TipoDeColumna.Fecha), new("Categoría"), new("Sentido"), new("Proyectado", TipoDeColumna.Moneda), new("Origen"),
            new("Real", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda),
        };
        if (!mio.Total)
            return new TablaExportable("Flujo de caja", Periodo(desde, hasta), columnas, [], null,
                ["El flujo es de toda la cooperativa: lo ve quien tiene todas las cuentas a su alcance (Treasury.Scope.All)."]);
        var meses = (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month + 1;
        var flujo = await sender.Send(new GetCashFlowQuery(CashFlowPeriodKind.Month, desde, Math.Max(1, meses)), ct);
        if (flujo.IsFailure) return TablaExportable.Vacia("Flujo de caja", Periodo(desde, hasta), columnas);
        var filas = new List<FilaExportable>();
        foreach (var p in flujo.Value.Periods)
        {
            filas.AddRange(p.Lines.Select(l => new FilaExportable(
            [
                p.Start, p.End, $"{l.Code} · {l.Name}", l.Direction == CashFlowDirection.Inflow ? "Entrada" : "Salida", l.Projected, l.Automatic ? "Calculado" : "Digitado", null, null,
            ])));
            var proyectado = p.ProjectedInflows - p.ProjectedOutflows;
            decimal? real = p.RealInflows is { } e && p.RealOutflows is { } s ? e - s : null;
            filas.Add(new FilaExportable(
            [
                p.Start, p.End, "Neto del período", null, proyectado, null, real, real is { } r ? r - proyectado : null,
            ], Resaltada: true));
        }
        var notas = new List<string>
        {
            "Lo real son los movimientos contabilizados de las cuentas bancarias propias: el de un mes cerrado coincide con Contabilidad. Los traslados entre cuentas propias "
            + "aparecen como entrada y salida (FR-138).",
        };
        notas.AddRange(flujo.Value.Missing.Select(m => m.Reason));
        return new TablaExportable("Flujo de caja", Periodo(desde, hasta), columnas, filas, null, notas);
    }
}
