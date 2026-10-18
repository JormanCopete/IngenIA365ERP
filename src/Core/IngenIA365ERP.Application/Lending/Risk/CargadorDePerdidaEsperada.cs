using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// Lo que el modelo de pérdida esperada necesita de un crédito además de lo que cargó <see cref="CargadorDeRiesgo"/> (feature 013, K6a, T1036): su
/// historial de mora construido (<see cref="HistorialDeMora.Construir"/> con la cadena de novaciones y refinanciaciones), la mora de los meses que
/// cubren las transacciones, la mora al corte de la migración (regla «DelCorte»), si el deudor es asociado activo en el mes (EA), la cuota del mes
/// (VALCUOTA) y los tipos de garantía candidatos de la PDI. (nuevo)
/// </summary>
public sealed record DatosDePerdidaEsperada(
    HistorialDeMoraConstruido Historial,
    IReadOnlyDictionary<(int Anio, int Mes), int> MoraDeTransacciones,
    int? DiasDelCorte,
    bool AsociadoActivo,
    decimal ValorDeLaCuota,
    IReadOnlyList<string> TiposDeGarantia);

/// <summary>
/// La carga en bloque de los datos de la pérdida esperada (feature 013, K6a, T1036) para los créditos de un cierre cuyo segmento está en pérdida
/// esperada. Decisiones del bloque 2 (pendientes de validar con la contadora y el dueño):
/// <list type="bullet">
/// <item><b>EA</b> (asociado activo en el mes de evaluación) sale del maestro de asociados (<c>COR_Associates</c>): activo al último día del mes si
/// ingresó hasta esa fecha y no se retiró, o reingresó después de retirarse. Sin ficha de asociado, no activo: EA = 0, que en el modelo es lo
/// conservador (su coeficiente baja el puntaje). Cuando Aportes y Asociados publiquen su consulta, se lee de allí.</item>
/// <item><b>VALCUOTA</b>: la cuota (total) del plan vigente a R que vence en el mes; si no vence ninguna, la siguiente; sin plan, cero.</item>
/// <item><b>Mora de los meses que cubren las transacciones</b>: la mayor de las posiciones diarias del mes conocidas a R; el mes del corte de la
/// migración, la mayor entre esas y la mora migrada al corte (la línea del cierre base); el mes que se cierra, la mayor del mes de la carga de
/// riesgo. Un mes sin dato toma cero (no debería pasar: el ciclo diario escribe una posición por día).</item>
/// <item><b>Garantías</b>: <see cref="TipoDeGarantiaDePdi"/> (varias garantías: la PDI más conservadora).</item>
/// </list>
/// No escribe. (nuevo)
/// </summary>
public sealed class CargadorDePerdidaEsperada(IApplicationDbContext db)
{
    public async Task<IReadOnlyDictionary<int, DatosDePerdidaEsperada>> CargarAsync(IReadOnlyCollection<CreditoDeRiesgo> creditos, DateOnly fin,
        DateTime? informacionHasta, int meses, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(creditos);
        if (creditos.Count == 0) return new Dictionary<int, DatosDePerdidaEsperada>();
        var r = informacionHasta ?? DateTime.MaxValue;
        var ids = creditos.Select(c => c.Credito.Id).ToList();
        var inicioDelMes = new DateOnly(fin.Year, fin.Month, 1);
        var desde = inicioDelMes.AddMonths(-(meses - 1));

        var cadenas = await CompletitudDelHistorial.CadenasAsync(db, creditos.Select(c => c.Credito).ToList(), ct);
        var deLasCadenas = cadenas.Values.SelectMany(c => c.Antecesores.Append(c.LoanId)).Distinct().ToList();
        var entradas = await CompletitudDelHistorial.EntradasAsync(db, deLasCadenas, informacionHasta, ct);
        var cargas = await db.MigrationLoads.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(l => l.Id, l => l.CutoffDate, ct);

        var porMes = (await db.LoanDailyPositions.AsNoTracking()
                .Where(p => ids.Contains(p.LoanId) && p.PositionDate >= desde && p.PositionDate <= fin && p.RegisteredAt <= r)
                .Select(p => new { p.LoanId, p.PositionDate, p.DaysPastDue }).ToListAsync(ct))
            .GroupBy(p => (p.LoanId, p.PositionDate.Year, p.PositionDate.Month))
            .ToDictionary(g => g.Key, g => g.Max(p => p.DaysPastDue));

        var base_ = await db.MonthlyCloses.AsNoTracking().Where(c => c.Kind == MonthlyCloseKind.Base && !c.IsDeleted).Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        var delBase = base_ is not { } baseId
            ? new Dictionary<int, int>()
            : await db.MonthlyCloseLoans.AsNoTracking().Where(l => l.MonthlyCloseId == baseId && ids.Contains(l.LoanId))
                .ToDictionaryAsync(l => l.LoanId, l => l.DaysPastDueAtClose, ct);

        var deudores = creditos.Select(c => c.Credito.DebtorPersonId).Distinct().ToList();
        var asociados = (await db.Associates.AsNoTracking().Where(a => deudores.Contains(a.PersonId) && !a.IsDeleted).ToListAsync(ct))
            .GroupBy(a => a.PersonId)
            .ToDictionary(g => g.Key, g => g.Any(a => (a.JoinDate is not { } ingreso || ingreso <= fin)
                && (a.WithdrawalDate is not { } retiro || retiro > fin || (a.RejoinDate is { } reingreso && reingreso >= retiro && reingreso <= fin))));

        var planes = (await db.RepaymentScheduleVersions.AsNoTracking()
                .Where(v => ids.Contains(v.LoanId) && v.RegisteredAt <= r && v.EffectiveValueDate <= fin).ToListAsync(ct))
            .GroupBy(v => v.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First().Id);
        var planIds = planes.Values.ToList();
        var cuotas = (await db.RepaymentInstallments.AsNoTracking()
                .Where(i => planIds.Contains(i.ScheduleVersionId) && !i.IsExtraordinary && i.DueDate >= inicioDelMes)
                .Select(i => new { i.ScheduleVersionId, i.DueDate, i.Total }).ToListAsync(ct))
            .GroupBy(i => i.ScheduleVersionId).ToDictionary(g => g.Key, g => g.OrderBy(i => i.DueDate).First().Total);

        var garantias = (await db.Collaterals.AsNoTracking()
                .Where(g => g.LoanId != null && ids.Contains(g.LoanId.Value) && !g.IsDeleted && g.ConstitutedOn <= fin).ToListAsync(ct))
            .GroupBy(g => g.LoanId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => new GarantiaParaPdi(x.Kind, x.PerfeccionadaA(fin))).ToList());

        var resultado = new Dictionary<int, DatosDePerdidaEsperada>();
        foreach (var c in creditos)
        {
            var id = c.Credito.Id;
            DateOnly? corteDeLaCarga = c.Credito.MigrationLoadId is { } carga && cargas.TryGetValue(carga, out var cc) ? cc : null;
            var corte = CompletitudDelHistorial.CorteParaHistorial(corteDeLaCarga);
            var historial = HistorialDeMora.Construir(cadenas.TryGetValue(id, out var cadena) ? cadena : new CreditoParaHistorial(id, c.Credito.OpenedOn),
                entradas, corte, fin, meses);

            var transacciones = new Dictionary<(int, int), int>();
            foreach (var m in historial.Meses.Where(m => m.CubiertoPorTransacciones))
                transacciones[(m.Anio, m.Mes)] = porMes.GetValueOrDefault((id, m.Anio, m.Mes));
            if (corteDeLaCarga is { } cd && delBase.TryGetValue(id, out var alCorte) && transacciones.TryGetValue((cd.Year, cd.Month), out var delMesDelCorte))
                transacciones[(cd.Year, cd.Month)] = Math.Max(delMesDelCorte, alCorte);
            transacciones[(fin.Year, fin.Month)] = Math.Max(transacciones.GetValueOrDefault((fin.Year, fin.Month)), c.MaxDiasDeMoraDelMes);

            var cuota = planes.TryGetValue(id, out var plan) ? cuotas.GetValueOrDefault(plan) : 0m;
            resultado[id] = new DatosDePerdidaEsperada(historial, transacciones, delBase.TryGetValue(id, out var d) ? d : null,
                asociados.GetValueOrDefault(c.Credito.DebtorPersonId), cuota,
                TipoDeGarantiaDePdi.Candidatos(garantias.GetValueOrDefault(id) ?? [], c.Codeudores.Count > 0));
        }
        return resultado;
    }
}
