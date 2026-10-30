using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Receipts.Sessions.Tareas;

// Feature 015, US2 (T0095; FR-019, FR-034, FR-036, FR-174, FR-185; TR43): las dos tareas de la caja, cooperativa por cooperativa como «Proceso de la
// cooperativa» por IEjecutorEnCooperativa. Sólo marcan, comparan y alertan: no emiten mensajes de negocio ni mueven dinero. Sin dependencias en
// ProgramadorDeTareas (no las tiene): el cuadre espera la marca de corrida del lote del día, y el orden con el ciclo de Cartera es la restricción de
// configuración de Recaudo.HoraDeCorte (ReglasDeParametrosDeRecaudo). (nuevo)

/// <summary>
/// La marca de corrida del lote del día de Recaudo (feature 015, T0095): la deja <c>recaudo.lote-del-dia</c> (fase 16, T0263) en <c>COR_SystemSettings</c> con
/// la última fecha cuyo lote terminó. Sin contabilidad iniciada no hay lote que esperar. (nuevo)
/// </summary>
public static class MarcaDelLoteDelDia
{
    public const string Clave = "RCD.LoteDelDia.Terminado";

    /// <summary>¿Terminó el lote de <paramref name="dia"/>?</summary>
    public static async Task<bool> TerminoAsync(IApplicationDbContext db, IContabilidadParaRecaudo contabilidad, DateOnly dia, CancellationToken ct)
    {
        if (!(await contabilidad.EstadoDelPeriodoAsync(dia, ct)).AccountingInitialized) return true;
        var valor = await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var terminado) && terminado >= dia;
    }

    /// <summary>Deja la marca de <paramref name="dia"/> (la llama el lote del día). Sin guardar.</summary>
    public static async Task RegistrarAsync(IApplicationDbContext db, DateOnly dia, CancellationToken ct)
    {
        var fila = await db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == Clave, ct);
        if (fila is null)
        {
            fila = new SystemSetting { SettingKey = Clave, ValueType = "Date", ModulePrefix = CatalogoDeRecaudo.Modulo, Description = "Último día cuyo lote de Recaudo terminó." };
            db.SystemSettings.Add(fila);
        }
        fila.SettingValue = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Marca las sesiones olvidadas (FR-034): una sesión abierta de un día anterior, o del día una vez pasada la hora de corte (<c>Recaudo.HoraDeCorte</c>), queda
/// <c>ForgottenOpen</c> y ya no recibe operaciones; se alerta <c>Recaudo.SesionOlvidada</c> para que el supervisor la cierre con testigo. (nuevo)
/// </summary>
public sealed class MarcaDeSesionesOlvidadas(IApplicationDbContext db, ParametrosDeCaja parametros, IDateTimeService reloj, IAlertas alertas)
{
    public async Task<int> MarcarAsync(CancellationToken ct)
    {
        var ahora = reloj.AhoraLocal;
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var corte = await parametros.HoraAsync(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, hoy, ct);
        var hastaElDia = TimeOnly.FromDateTime(ahora.DateTime) >= corte ? hoy : hoy.AddDays(-1);
        var olvidadas = await (from s in db.ReceiptsCashSessions
                               join d in db.CollectionPointDays on s.CollectionPointDayId equals d.Id
                               where s.Status == CashSessionState.Open && d.BusinessDate <= hastaElDia
                               select s).ToListAsync(ct);
        foreach (var s in olvidadas)
        {
            s.Status = CashSessionState.ForgottenOpen;
            s.IsForgotten = true;
        }
        await db.SaveChangesAsync(ct);
        foreach (var s in olvidadas)
        {
            var caja = await db.CashBoxes.AsNoTracking().Where(c => c.Id == s.CashBoxId).Select(c => c.Code).FirstAsync(ct);
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoSesionOlvidada, "Sesión de caja olvidada",
                $"La sesión de la caja {caja} quedó abierta después de la hora de corte: ciérrela con arqueo diferido y testigo.",
                "CashSession", s.PublicId, DedupKey: $"Recaudo.SesionOlvidada:{s.PublicId:N}", ModuloDeOrigen: "Receipts"), ct);
        }
        return olvidadas.Count;
    }
}

/// <summary>
/// El cuadre diario por oficina (FR-036, FR-185): compara el efectivo de las cajas (lo contado de las cerradas, lo esperado de las abiertas), de la bóveda y
/// en tránsito contra el saldo contable de la caja de la oficina (<c>SaldoContableAsync</c>, concepto <see cref="ConceptoCaja"/>) más lo emitido y pendiente
/// de lote, y verifica que el saldo de la bóveda por denominación coincida con sus movimientos. La diferencia se muestra, se puede explicar y se alerta
/// (<c>Recaudo.CuadreDiarioConDiferencia</c>). Repetirlo el mismo día actualiza la fila, salvo que ya esté explicada. (nuevo)
/// </summary>
public sealed class CuadreDiarioDeRecaudo(IApplicationDbContext db, IContabilidadParaRecaudo contabilidad, IAlertas alertas)
{
    public const string ConceptoCaja = "Caja";

    public async Task<IReadOnlyList<DailySquare>> CuadrarAsync(DateOnly dia, CancellationToken ct)
    {
        var oficinas = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                              join d in db.CollectionPointDays.AsNoTracking() on s.CollectionPointDayId equals d.Id
                              join p in db.CollectionPoints.AsNoTracking() on d.CollectionPointId equals p.Id
                              where d.BusinessDate == dia
                              select p.BranchId)
            .Union(db.Vaults.AsNoTracking().Select(v => v.BranchId)).Distinct().ToListAsync(ct);
        var cuadres = new List<DailySquare>();
        foreach (var oficina in oficinas.OrderBy(o => o))
        {
            var enCajas = await EfectivoEnCajasAsync(oficina, dia, ct);
            var boveda = await db.Vaults.AsNoTracking().FirstOrDefaultAsync(v => v.BranchId == oficina, ct);
            var enBoveda = boveda is null ? 0m : await MovimientosDeBoveda.SaldoAsync(db, boveda.Id, ct);
            var cuadraLaBoveda = boveda is null || (await MovimientosDeBoveda.DenominacionesQueNoCuadranAsync(db, boveda.Id, ct)).Count == 0;
            var oficinaPublicId = await db.Branches.AsNoTracking().Where(b => b.Id == oficina).Select(b => b.PublicId).FirstAsync(ct);
            var saldos = await contabilidad.SaldoContableAsync(new SolicitudDeSaldo(ConceptoCaja, dia, oficinaPublicId, null), ct);
            decimal? contable = saldos.Count == 0 ? null : saldos.Sum(s => s.Balance);
            const decimal enTransito = 0m; // las remesas en tránsito llegan con Tesorería (fase 7)
            const decimal pendienteDeLote = 0m; // lo emitido y pendiente de lote llega con los recibos (fases 6 y 16)
            var diferencia = contable is { } c ? enCajas + enBoveda + enTransito - (c + pendienteDeLote) : 0m;

            var fila = await db.DailySquares.FirstOrDefaultAsync(q => q.BranchId == oficina && q.BusinessDate == dia, ct);
            if (fila is null)
            {
                fila = new DailySquare { BranchId = oficina, BusinessDate = dia };
                db.DailySquares.Add(fila);
            }
            else if (fila.Status == DailySquareStatus.Explained)
            {
                cuadres.Add(fila);
                continue;
            }
            fila.CashInBoxes = enCajas;
            fila.CashInVault = enBoveda;
            fila.CashInTransit = enTransito;
            fila.LedgerBalance = contable;
            fila.EmittedPendingBatch = pendienteDeLote;
            fila.Difference = diferencia;
            fila.VaultBalanceMatches = cuadraLaBoveda;
            fila.Status = diferencia != 0m || !cuadraLaBoveda ? DailySquareStatus.Difference : DailySquareStatus.Squared;
            fila.Explanation = contable is null ? "Sin saldo contable de la caja: Contabilidad no lo informó (sin matriz o sin contabilidad iniciada)." : null;
            cuadres.Add(fila);
        }
        await db.SaveChangesAsync(ct);
        foreach (var f in cuadres.Where(f => f.Status == DailySquareStatus.Difference))
        {
            var oficinaPublicId = await db.Branches.AsNoTracking().Where(b => b.Id == f.BranchId).Select(b => b.PublicId).FirstAsync(ct);
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoCuadreDiarioConDiferencia, "Cuadre diario con diferencia",
                string.Create(CultureInfo.InvariantCulture,
                    $"El cuadre del {f.BusinessDate:dd/MM/yyyy} tiene una diferencia de {f.Difference:N0}{(f.VaultBalanceMatches ? string.Empty : " y la bóveda no cuadra por denominación")}."),
                "DailySquare", f.PublicId, DedupKey: $"Recaudo.CuadreDiarioConDiferencia:{f.BranchId}:{f.BusinessDate:yyyyMMdd}", ModuloDeOrigen: "Receipts",
                ScopeBranchPublicId: oficinaPublicId), ct);
        }
        return cuadres;
    }

    /// <summary>El efectivo de las cajas de la oficina en el día: lo contado en el cierre de las cerradas, lo esperado de las demás.</summary>
    private async Task<decimal> EfectivoEnCajasAsync(int oficina, DateOnly dia, CancellationToken ct)
    {
        var sesiones = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                              join d in db.CollectionPointDays.AsNoTracking() on s.CollectionPointDayId equals d.Id
                              join p in db.CollectionPoints.AsNoTracking() on d.CollectionPointId equals p.Id
                              where d.BusinessDate == dia && p.BranchId == oficina
                              select s).ToListAsync(ct);
        var total = 0m;
        foreach (var s in sesiones)
        {
            var sellados = ValoresSelladosDeLaSesion.De(s);
            if (sellados.CashMeansId is not { } efectivo) continue;
            if (s.Status is CashSessionState.Closed or CashSessionState.ClosedWithDifferencePending)
                total += await (from l in db.ReceiptsCashCountLines.AsNoTracking()
                                join c in db.ReceiptsCashCounts.AsNoTracking() on l.CashCountId equals c.Id
                                where c.CashSessionId == s.Id && c.Kind != CashCountKind.Surprise && l.PaymentMeansId == efectivo
                                orderby c.Id descending
                                select l.Counted).FirstOrDefaultAsync(ct);
            else
                total += await SesionesDeCaja.EfectivoAsync(db, s, ct);
        }
        return total;
    }
}

/// <summary>
/// La tarea <c>recaudo.sesiones-olvidadas</c>: cada <see cref="Intervalo"/> marca las sesiones que cruzaron la hora de corte de su cooperativa. (nuevo)
/// </summary>
public sealed class TareaDeSesionesOlvidadas : ITareaProgramada
{
    public const string NombreDeLaTarea = "recaudo.sesiones-olvidadas";

    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<MarcaDeSesionesOlvidadas>().MarcarAsync(ct);
}

/// <summary>
/// La tarea <c>recaudo.cuadre-diario</c>: cada <see cref="Intervalo"/>, pasada la hora de corte de la cooperativa y con el lote del día terminado
/// (<see cref="MarcaDelLoteDelDia"/>), cuadra el día; si no, espera a la siguiente pasada. (nuevo)
/// </summary>
public sealed class TareaDeCuadreDiario : ITareaProgramada
{
    public const string NombreDeLaTarea = "recaudo.cuadre-diario";

    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var reloj = servicios.GetRequiredService<IDateTimeService>();
        var parametros = servicios.GetRequiredService<ParametrosDeCaja>();
        var db = servicios.GetRequiredService<IApplicationDbContext>();
        var contabilidad = servicios.GetRequiredService<IContabilidadParaRecaudo>();
        var ahora = reloj.AhoraLocal;
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var corte = await parametros.HoraAsync(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, hoy, ct);
        if (TimeOnly.FromDateTime(ahora.DateTime) < corte) return;
        if (!await MarcaDelLoteDelDia.TerminoAsync(db, contabilidad, hoy, ct)) return;
        await servicios.GetRequiredService<CuadreDiarioDeRecaudo>().CuadrarAsync(hoy, ct);
    }
}
