using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Receipts.Tareas;

/// <summary>
/// La antigüedad de Recaudo (feature 015, US6, T0197; FR-073, FR-196; DT §2.5): <b>sólo alerta</b>, nunca mueve una partida ni un saldo
/// (<c>NingunProcesoAplicaPagosNiInformaResultados</c>). Cada sección es independiente y devuelve cuántas alertó:
/// <list type="bullet">
/// <item>partidas por identificar en cada escalón de <c>Partidas.EscalonesDias</c> (30, 60 y 90 por defecto; <c>Recaudo.PartidaAntigua</c>) y el consignante no
/// identificado que se repite (señal al oficial de cumplimiento);</item>
/// <item>saldos a favor de pagadores abiertos más de un mes (<c>Recaudo.SaldoAFavorAntiguo</c>, pendiente desde la fase 10);</item>
/// <item>cuentas de recaudo sin extracto en <c>Extractos.DiasHabilesSinCarga</c> días hábiles (<c>Recaudo.ExtractoSinCargar</c>);</item>
/// <item>partidas abiertas del mes en los últimos días hábiles antes del cierre mensual de Cartera (<c>Recaudo.PartidasAbiertasAlCierre</c>).</item>
/// </list>
/// Las secciones de contingencia, cuadre provisional y política en «advertir» las suman sus fases. (nuevo)
/// </summary>
public sealed class AntiguedadDeRecaudo(
    IApplicationDbContext db,
    ParametrosDeCaja parametros,
    IMovimientosBancariosParaRecaudo movimientos,
    ICalendarioHabil calendario,
    IAlertas alertas,
    IDateTimeService reloj)
{
    /// <summary>Los escalones por defecto (D10) si el parámetro no se puede leer.</summary>
    public static readonly IReadOnlyList<int> EscalonesPorDefecto = [30, 60, 90];

    /// <summary>La señal del consignante repetido va al oficial de cumplimiento (quien tiene el permiso de cumplimiento).</summary>
    public const string PermisoDelOficial = "Receipts.Compliance.View";

    /// <summary>Los días de antigüedad de un saldo a favor que alertan.</summary>
    public const int DiasDeSaldoAFavorAntiguo = 30;

    /// <summary>Los escalones de <c>Partidas.EscalonesDias</c> («30,60,90»), ordenados.</summary>
    public static async Task<IReadOnlyList<int>> EscalonesAsync(ParametrosDeCaja parametros, DateOnly fecha, CancellationToken ct)
    {
        var texto = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.PartidasEscalonesDias, fecha, ct);
        var escalones = (texto ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0).Where(d => d > 0).Distinct().Order().ToList();
        return escalones.Count > 0 ? escalones : EscalonesPorDefecto;
    }

    /// <summary>Todas las secciones de la tarea.</summary>
    public async Task<int> RevisarAsync(CancellationToken ct) =>
        await PartidasAsync(ct) + await SaldosAFavorAsync(ct) + await ExtractosSinCargarAsync(ct) + await PartidasAlCierreAsync(ct) + await ContingenciaPorDigitarAsync(ct)
        + await PoliticaContableEnAdvertirAsync(ct) + await CuadreProvisionalPendienteAsync(ct) + await ConsignacionesReportadasAsync(ct);

    /// <summary>
    /// Feature 015, R1 (fase 27; FR-069): las consignaciones reportadas en ventanilla que no aparecen en el extracto en <c>ConsignacionReportada.DiasParaVerificar</c> días
    /// (<c>Recaudo.ConsignacionReportadaSinVerificar</c>, una vez por consignación). Sólo avisa: no cambia la consignación ni aplica nada.
    /// </summary>
    public async Task<int> ConsignacionesReportadasAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ConsignacionReportadaDiasParaVerificar, hoy, ct);
        if (dias is not > 0) return 0;
        var limite = hoy.AddDays(-dias.Value);
        var vencidas = await db.ReportedDeposits.AsNoTracking()
            .Where(d => d.Status == ReportedDepositStatus.ToVerify && d.DepositDate < limite).ToListAsync(ct);
        foreach (var d in vencidas)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoConsignacionReportadaSinVerificar, "Consignación reportada sin encontrar en el extracto",
                string.Create(CultureInfo.InvariantCulture,
                    $"La consignación reportada {d.Number} del {d.DepositDate:dd/MM/yyyy} por {d.Amount:N0} no aparece en el extracto después de {dias} días: revísela con el banco."),
                "ReportedDeposit", d.PublicId, DedupKey: $"Recaudo.ConsignacionReportadaSinVerificar:{d.PublicId:N}", ModuloDeOrigen: "Receipts",
                ScopeBranchPublicId: await OficinaAsync(d.BranchId, ct)), ct);
        return vencidas.Count;
    }

    /// <summary>
    /// Feature 015, US24 (T0321; FR-228): mientras la activación de Recaudo siga con el cuadre de apertura provisional, la alerta <c>Recaudo.CuadreProvisionalPendiente</c>
    /// se repite cada corrida; vencida la fecha límite del definitivo escala (otra clave, también a <c>Platform.GoLive.Override</c>). El definitivo la cierra.
    /// </summary>
    public async Task<int> CuadreProvisionalPendienteAsync(CancellationToken ct) =>
        await IngenIA365ERP.Application.Common.Cuadres.CuadreProvisionalPendiente.AlertarAsync(alertas, TiposDeAlerta.RecaudoCuadreProvisionalPendiente, "Recaudo", "Receipts", "ReceiptsOpeningSquare",
            await Activation.EstadoOperativoDeRecaudoParaElCierre.PendienteAsync(db, ct), reloj.HoyLocal, ct);

    /// <summary>
    /// Feature 015, US8 (T0262; FR-221): mientras <c>Contabilidad.FaltaDeRegla</c> de Recaudo esté en «advertir» se confirman operaciones sin regla contable; la
    /// alerta <c>Recaudo.PoliticaContableEnAdvertir</c> se repite cada día (una por día) hasta que vuelva a «bloquear».
    /// </summary>
    public async Task<int> PoliticaContableEnAdvertirAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var politica = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla, hoy, ct);
        if (!string.Equals(politica, "Advertir", StringComparison.OrdinalIgnoreCase)) return 0;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoPoliticaContableEnAdvertir, "Política contable de Recaudo en «advertir»",
            "Recaudo confirma operaciones aunque la matriz contable no tenga su regla: complete la matriz y vuelva la política a «bloquear».",
            DedupKey: string.Create(CultureInfo.InvariantCulture, $"Recaudo.PoliticaContableEnAdvertir:{hoy:yyyy-MM-dd}"), ModuloDeOrigen: "Receipts"), ct);
        return 1;
    }

    /// <summary>
    /// Feature 015, US2 (T0243; FR-186): los talonarios de contingencia con números sin digitar por debajo del último digitado (un recibo del papel que todavía no
    /// entró al sistema): una alerta por talonario con los números que faltan. Sólo informa.
    /// </summary>
    public async Task<int> ContingenciaPorDigitarAsync(CancellationToken ct)
    {
        var talonarios = await db.ContingencyBooks.AsNoTracking().Where(b => b.Status == ContingencyBookState.Active).ToListAsync(ct);
        var alertadas = 0;
        foreach (var t in talonarios)
        {
            var usados = await db.IssuedReceipts.AsNoTracking().Where(r => r.ContingencyBookId == t.Id && r.ContingencyNumber != null)
                .Select(r => r.ContingencyNumber!.Value).ToListAsync(ct);
            if (usados.Count == 0) continue;
            var faltan = Enumerable.Range(t.FromNumber, usados.Max() - t.FromNumber + 1).Except(usados).ToList();
            if (faltan.Count == 0) continue;
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoContingenciaPorDigitar, "Recibos de contingencia por digitar",
                string.Create(CultureInfo.InvariantCulture,
                    $"El talonario {t.FromNumber}–{t.ToNumber} tiene {faltan.Count} recibo(s) sin digitar antes del {usados.Max()}: {string.Join(", ", faltan.Take(20))}."),
                "ContingencyBook", t.PublicId, DedupKey: string.Create(CultureInfo.InvariantCulture, $"Recaudo.ContingenciaPorDigitar:{t.PublicId:N}:{faltan.Count}"),
                ModuloDeOrigen: "Receipts"), ct);
            alertadas++;
        }
        return alertadas;
    }

    /// <summary>Las partidas en cada escalón y el consignante repetido. Deja anotado el último escalón alertado de cada partida (sólo esa marca).</summary>
    public async Task<int> PartidasAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var escalones = await EscalonesAsync(parametros, hoy, ct);
        var abiertas = await db.UnidentifiedItems
            .Where(p => p.Status == UnidentifiedItemState.Open || p.Status == UnidentifiedItemState.PartiallyIdentified).ToListAsync(ct);
        var alertadas = 0;
        foreach (var p in abiertas)
        {
            var dias = hoy.DayNumber - p.OpenedOn.DayNumber;
            var escalon = escalones.Where(e => e <= dias).DefaultIfEmpty(0).Max();
            if (escalon == 0 || escalon <= p.AgingStep) continue;
            p.AgingStep = (short)escalon;
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoPartidaAntigua, $"Partida por identificar de {escalon} días",
                string.Create(CultureInfo.InvariantCulture,
                    $"La partida {p.Number} ({p.Balance:N0}, consignada el {p.MovementDate:dd/MM/yyyy}{(p.DepositorName is null ? string.Empty : $" por {p.DepositorName}")}) sigue sin identificar."),
                "UnidentifiedItem", p.PublicId, DedupKey: $"Recaudo.PartidaAntigua:{p.PublicId:N}:{escalon}", ModuloDeOrigen: "Receipts",
                ScopeBranchPublicId: await OficinaAsync(p.BranchId, ct)), ct);
            alertadas++;
        }
        // Cuántas partidas abiertas del mismo consignante levantan la señal: parámetro con vigencia (Sarlaft.ConsignanteRepetido.Partidas, pendiente de P7).
        var consignanteRepetido = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.SarlaftConsignanteRepetidoPartidas, hoy, ct) ?? 0;
        foreach (var g in abiertas.Where(p => consignanteRepetido > 0 && !string.IsNullOrWhiteSpace(p.DepositorTaxId)).GroupBy(p => p.DepositorTaxId!)
                     .Where(g => g.Count() >= consignanteRepetido))
        {
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoPartidaAntigua, "Consignante no identificado que se repite",
                string.Create(CultureInfo.InvariantCulture,
                    $"El consignante {g.Key} tiene {g.Count()} partidas sin identificar ({g.Sum(p => p.Balance):N0}): revíselo con el oficial de cumplimiento."),
                "UnidentifiedItem", g.OrderBy(p => p.Id).First().PublicId, DedupKey: $"Recaudo.ConsignanteRepetido:{g.Key}:{g.Count()}",
                RecipientPermissions: [PermisoDelOficial], ModuloDeOrigen: "Receipts"), ct);
            alertadas++;
        }
        await db.SaveChangesAsync(ct);
        return alertadas;
    }

    /// <summary>Los saldos a favor de pagadores abiertos más de <see cref="DiasDeSaldoAFavorAntiguo"/> días.</summary>
    public async Task<int> SaldosAFavorAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var limite = hoy.AddDays(-DiasDeSaldoAFavorAntiguo);
        var viejos = await db.PayerCreditBalances.AsNoTracking()
            .Where(s => s.Status == PayerCreditBalanceState.Open && s.Balance > 0m && s.OpenedOn <= limite).ToListAsync(ct);
        foreach (var s in viejos)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoSaldoAFavorAntiguo, "Saldo a favor sin resolver",
                string.Create(CultureInfo.InvariantCulture,
                    $"El saldo a favor del documento {s.OriginDocumentNumber} ({s.Balance:N0}) lleva {hoy.DayNumber - s.OpenedOn.DayNumber} días sin resolver: devuélvalo o aplíquelo."),
                "PayerCreditBalance", s.PublicId, DedupKey: $"Recaudo.SaldoAFavorAntiguo:{s.PublicId:N}", ModuloDeOrigen: "Receipts",
                ScopeBranchPublicId: await OficinaAsync(s.BranchId, ct)), ct);
        return viejos.Count;
    }

    /// <summary>Las cuentas de recaudo sin extracto en <c>Extractos.DiasHabilesSinCarga</c> días hábiles (FR-196).</summary>
    public async Task<int> ExtractosSinCargarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ExtractosDiasHabilesSinCarga, hoy, ct);
        if (dias is not { } limite || limite <= 0) return 0;
        var cargas = await movimientos.UltimasCargasAsync(ct);
        if (cargas.Count == 0) return 0;
        var habil = await calendario.ObtenerAsync(hoy.AddDays(-90), hoy, DiasDeDescanso.SabadoYDomingo, ct: ct);
        var alertadas = 0;
        foreach (var c in cargas)
        {
            var desde = c.LastStatementTo ?? hoy.AddDays(-90);
            if (habil.Contar(desde, hoy) < limite) continue;
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoExtractoSinCargar, "Cuenta de recaudo sin extracto",
                c.LastStatementTo is { } u
                    ? string.Create(CultureInfo.InvariantCulture, $"La cuenta {c.Code} no tiene extracto cargado después del {u:dd/MM/yyyy}: cárguelo para identificar sus abonos.")
                    : $"La cuenta {c.Code} nunca ha tenido extracto cargado: cárguelo para identificar sus abonos.",
                "OwnBankAccount", c.OwnBankAccountPublicId, DedupKey: string.Create(CultureInfo.InvariantCulture, $"Recaudo.ExtractoSinCargar:{c.OwnBankAccountPublicId:N}:{hoy:yyyyMMdd}"),
                ModuloDeOrigen: "Receipts", ScopeBranchPublicId: c.BranchPublicId), ct);
            alertadas++;
        }
        return alertadas;
    }

    /// <summary>En los dos últimos días hábiles del mes, las partidas abiertas del mes antes del cierre mensual de Cartera (FR-196).</summary>
    public async Task<int> PartidasAlCierreAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var finDeMes = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        var habil = await calendario.ObtenerAsync(hoy, finDeMes, DiasDeDescanso.SabadoYDomingo, ct: ct);
        if (habil.Contar(hoy, finDeMes) > 2) return 0;
        var inicio = new DateOnly(hoy.Year, hoy.Month, 1);
        var delMes = await db.UnidentifiedItems.AsNoTracking()
            .Where(p => (p.Status == UnidentifiedItemState.Open || p.Status == UnidentifiedItemState.PartiallyIdentified) && p.MovementDate >= inicio && p.MovementDate <= hoy)
            .Select(p => new { p.Balance }).ToListAsync(ct);
        if (delMes.Count == 0) return 0;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoPartidasAbiertasAlCierre, "Partidas abiertas antes del cierre de Cartera",
            string.Create(CultureInfo.InvariantCulture,
                $"Quedan {delMes.Count} partidas del mes sin identificar ({delMes.Sum(p => p.Balance):N0}) antes del cierre mensual de Cartera: identifíquelas para que el cierre las vea aplicadas."),
            "UnidentifiedItem", null, DedupKey: string.Create(CultureInfo.InvariantCulture, $"Recaudo.PartidasAbiertasAlCierre:{hoy:yyyyMMdd}"), ModuloDeOrigen: "Receipts"), ct);
        return 1;
    }

    private async Task<Guid?> OficinaAsync(int branchId, CancellationToken ct) =>
        await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
}

/// <summary>La tarea <c>recaudo.antiguedad</c>: una vez al día (cada <see cref="Intervalo"/>), por cooperativa; sólo alerta. (nuevo)</summary>
public sealed class TareaDeAntiguedadDeRecaudo : ITareaProgramada
{
    public const string NombreDeLaTarea = "recaudo.antiguedad";

    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<AntiguedadDeRecaudo>().RevisarAsync(ct);
}
