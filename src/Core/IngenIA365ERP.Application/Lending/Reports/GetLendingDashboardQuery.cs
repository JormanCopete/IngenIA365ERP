using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports;

/// <summary>A dónde lleva una ficha del tablero con un clic: la página y su query (api.md §17, «Tablero»). (nuevo)</summary>
public sealed record LendingDashboardLinkDto(string Page, IReadOnlyDictionary<string, string> Query);

/// <summary>
/// Una ficha del tablero de Cartera (api.md §17): <c>unit</c> es <c>Money | Count | Percent | Days</c> y <c>severity</c> usa los nombres de
/// <see cref="AlertSeverity"/>. <c>value</c> es nulo cuando no hay dato (sin cierre, sin cobranza); <c>note</c> dice por qué o cómo se lee. Sólo
/// cifras: nunca una persona, un documento ni un crédito (<c>Lending.Dashboard.View</c> es de los <c>View</c>, T60, DT §2.25). (nuevo)
/// </summary>
public sealed record LendingDashboardTileDto(
    string Key,
    string Label,
    decimal? Value,
    decimal? PreviousValue,
    string Unit,
    string? Severity,
    LendingDashboardLinkDto Link,
    string? Note = null);

/// <summary>Una oficina o una línea que el tablero puede filtrar. (nuevo)</summary>
public sealed record LendingDashboardScopeItemDto(Guid PublicId, string Code, string Name);

/// <summary>Las oficinas y las líneas del alcance de quien consulta, para los filtros de la pantalla. (nuevo)</summary>
public sealed record LendingDashboardScopeDto(IReadOnlyList<LendingDashboardScopeItemDto> Branches, IReadOnlyList<LendingDashboardScopeItemDto> Lines);

/// <summary>El tablero de Cartera (api.md §17, <c>LendingDashboardDto</c>). (nuevo)</summary>
public sealed record LendingDashboardDto(DateOnly AsOf, LendingDashboardScopeDto Scope, IReadOnlyList<LendingDashboardTileDto> Tiles);

/// <summary>
/// <c>GET /api/lending/dashboard?branch=&amp;line=&amp;asOf=</c> (feature 013, US15, K5, T0983; FR-135; US15-1; contracts/api.md §17),
/// <c>Lending.Dashboard.View</c>: las fichas del tablero de Cartera a <c>asOf</c> (hoy en la fecha local por defecto), de la oficina y la línea
/// pedidas, siempre dentro del alcance de quien consulta (<see cref="IAlcanceDeCartera"/>): una oficina o una línea fuera de él da cifras en cero,
/// nunca las de otra. (nuevo)
/// </summary>
public sealed record GetLendingDashboardQuery(Guid? Branch = null, Guid? Line = null, DateOnly? AsOf = null) : IRequest<Result<LendingDashboardDto>>;

/// <summary>
/// El tablero (T0983; FR-135). Cada ficha trae <c>previousValue</c> del mes anterior y su enlace a la vista o la pantalla que la explica:
/// <list type="bullet">
/// <item><c>saldo</c>: capital de las posiciones diarias al corte (créditos vivos no castigados); el anterior, al cierre del mes anterior.</item>
/// <item><c>colocacion-del-mes</c>: desembolsos sin reversar del primero del mes al corte; el anterior, el mismo tramo del mes anterior.</item>
/// <item><c>calidad</c>: vencida sobre bruta (%) de las posiciones al corte, con <c>Informes.DiasCarteraVencida</c>.</item>
/// <item><c>deterioro</c>: individual más general del último cierre vigente al corte (<see cref="QualityReportQueryHandler.CalidadAsync"/>); el
/// anterior, el del cierre vigente que lo precede.</item>
/// <item><c>recaudo-vs-esperado</c>: pagos sin reversar del mes hasta el corte sobre lo exigible del mes hasta el corte según los planes vigentes (la
/// cuota de la versión más reciente del plan que la regeneró), en %.</item>
/// <item><c>cosechas</c>: capital con 30 días de mora o más al corte de los créditos desembolsados en los últimos doce meses, sobre lo desembolsado
/// (%; el detalle por mes, en la vista <c>vintages</c>).</item>
/// <item><c>cumplimiento-de-promesas</c>: promesas cumplidas sobre las que ya se resolvieron (cumplidas más incumplidas) de las que vencían en el
/// mes hasta el corte (%); antes de K4, vacía con «disponible con cobranza».</item>
/// <item><c>mensajes-pendientes-y-rechazados</c>: las entregas de la bandeja de Cartera pendientes, en lote, rechazadas o que no pasaron la
/// validación, con la vista de la bandeja (<see cref="IVistaDeBandejaDelModulo"/>), que aplica su propio alcance.</item>
/// <item><c>alertas-pendientes</c>: las alertas de Cartera sin atender que quien consulta puede ver (<see cref="VisibilidadDeAlertas"/>).</item>
/// </list>
/// Las definiciones de calidad y cosecha son las de las vistas y están <b>a validar por la contadora</b>. (nuevo)
/// </summary>
public sealed class GetLendingDashboardQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ReglasDeParametrosDeCartera parametros,
    IDateTimeService reloj,
    IEnumerable<IVistaDeBandejaDelModulo> vistasDeBandeja,
    VisibilidadDeAlertas? alertas = null,
    EntregaDeCartera? entregaVigente = null) : IRequestHandler<GetLendingDashboardQuery, Result<LendingDashboardDto>>
{
    public const string PaginaDeInformes = "/cartera/informes";
    public const string PaginaDeMensajes = "/cartera/bandeja-de-mensajes";
    public const string PaginaDeAlertas = "/cartera/alertas";

    public const string Dinero = "Money";
    public const string Conteo = "Count";
    public const string Porcentaje = "Percent";

    /// <summary>La mora desde la que cuenta la ficha de cosechas (la primera columna de la vista <c>vintages</c>).</summary>
    public const int MoraDeLaCosecha = 30;

    /// <summary>Los meses de desembolso que mira la ficha de cosechas.</summary>
    public const int MesesDeLaCosecha = 12;

    public const string SinCobranza = "Disponible con cobranza";

    private readonly EntregaDeCartera _entrega = entregaVigente ?? CatalogoDeCartera.EntregaVigente;

    public async Task<Result<LendingDashboardDto>> Handle(GetLendingDashboardQuery request, CancellationToken ct)
    {
        var corte = request.AsOf ?? reloj.HoyLocal;
        var filtros = new FiltrosDeInformeDeCartera { Branch = request.Branch, Line = request.Line, AsOf = corte };
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, filtros, ct);
        var inicioDelMes = new DateOnly(corte.Year, corte.Month, 1);
        var finDelMesAnterior = inicioDelMes.AddDays(-1);
        var inicioDelMesAnterior = inicioDelMes.AddMonths(-1);
        var mismoDiaDelMesAnterior = corte.AddMonths(-1);
        var dias = await QualityReportQueryHandler.DiasDeVencidaAsync(parametros, corte, ct);
        var diasAnterior = await QualityReportQueryHandler.DiasDeVencidaAsync(parametros, finDelMesAnterior, ct);
        var fichas = new List<LendingDashboardTileDto>();

        // Saldo y calidad: las posiciones diarias al corte y al cierre del mes anterior.
        var hoy = await PosicionesAsync(ambito, corte, ct);
        var antes = await PosicionesAsync(ambito, finDelMesAnterior, ct);
        fichas.Add(new("saldo", "Saldo de capital", hoy.Sum(p => p.Capital), antes.Sum(p => p.Capital), Dinero, null,
            Informe("aging", ("asOf", Fecha(corte)))));

        fichas.Add(new("colocacion-del-mes", "Colocación del mes", await ColocacionAsync(ambito, inicioDelMes, corte, ct),
            await ColocacionAsync(ambito, inicioDelMesAnterior, mismoDiaDelMesAnterior, ct), Dinero, null,
            Informe("placement", ("from", Fecha(inicioDelMes)), ("to", Fecha(corte)))));

        fichas.Add(new("calidad", "Cartera vencida sobre bruta", Vencida(hoy, dias), Vencida(antes, diasAnterior), Porcentaje, null,
            Informe("quality", ("asOf", Fecha(corte))), $"Vencida: más de {dias} días de mora."));

        var (deterioro, deterioroAnterior, notaDeterioro) = await DeterioroAsync(ambito, corte, ct);
        fichas.Add(new("deterioro", "Deterioro del último cierre", deterioro, deterioroAnterior, Dinero, null,
            Informe("quality", ("asOf", Fecha(corte))), notaDeterioro));

        var recaudo = await RecaudoSobreEsperadoAsync(ambito, inicioDelMes, corte, ct);
        var recaudoAnterior = await RecaudoSobreEsperadoAsync(ambito, inicioDelMesAnterior, finDelMesAnterior, ct);
        fichas.Add(new("recaudo-vs-esperado", "Recaudo frente al esperado", recaudo, recaudoAnterior, Porcentaje, null,
            Informe("receipts", ("from", Fecha(inicioDelMes)), ("to", Fecha(corte))), "Pagos del mes sobre lo exigible del mes hasta la fecha."));

        fichas.Add(new("cosechas", $"Mora {MoraDeLaCosecha} de las cosechas del año", await CosechaAsync(ambito, corte, ct),
            await CosechaAsync(ambito, finDelMesAnterior, ct), Porcentaje, null,
            Informe("vintages", ("from", Fecha(inicioDelMes.AddMonths(1 - MesesDeLaCosecha))), ("to", Fecha(corte))),
            $"Créditos desembolsados en los últimos {MesesDeLaCosecha} meses con {MoraDeLaCosecha} días de mora o más, sobre lo desembolsado."));

        if (_entrega < EntregaDeCartera.K4)
        {
            fichas.Add(new("cumplimiento-de-promesas", "Cumplimiento de promesas", null, null, Porcentaje, null, Informe("promises"), SinCobranza));
        }
        else
        {
            fichas.Add(new("cumplimiento-de-promesas", "Cumplimiento de promesas", await PromesasAsync(ambito, inicioDelMes, corte, ct),
                await PromesasAsync(ambito, inicioDelMesAnterior, finDelMesAnterior, ct), Porcentaje, null, Informe("promises"),
                "Promesas cumplidas sobre las que vencían en el mes y ya se resolvieron."));
        }

        var (mensajes, rechazados) = await MensajesAsync(ct);
        fichas.Add(new("mensajes-pendientes-y-rechazados", "Mensajes pendientes y rechazados", mensajes, null, Conteo,
            rechazados > 0 ? nameof(AlertSeverity.Warning) : null, new LendingDashboardLinkDto(PaginaDeMensajes, new Dictionary<string, string>()),
            mensajes is null ? "Sin la bandeja de mensajes de Cartera." : $"{rechazados} rechazados o con la validación fallida."));

        var pendientes = await AlertasAsync(ct);
        fichas.Add(new("alertas-pendientes", "Alertas pendientes", pendientes, null, Conteo,
            pendientes > 0 ? nameof(AlertSeverity.Warning) : null, new LendingDashboardLinkDto(PaginaDeAlertas, new Dictionary<string, string>())));

        return Result.Success(new LendingDashboardDto(corte, await AlcanceAsync(ambito.Alcance, ct), fichas));
    }

    // --------------------------------------------------------------------------------------------------------- fuentes --

    private sealed record Posicion(int LoanId, decimal Capital, int Dias);

    private async Task<IReadOnlyList<Posicion>> PosicionesAsync(AmbitoDeInformeDeCartera ambito, DateOnly fecha, CancellationToken ct)
    {
        var ids = await CargadorDeRiesgo.VivosAl(ambito.Creditos(db), fecha).Where(l => l.Status != LoanStatus.WrittenOff).Select(l => l.Id).ToListAsync(ct);
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, ids, fecha, ct);
        return posiciones.Values.Where(p => ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode))
            .Select(p => new Posicion(p.LoanId, p.Principal, p.DaysPastDue)).ToList();
    }

    private static decimal? Vencida(IReadOnlyList<Posicion> posiciones, int dias) =>
        QualityReportQueryHandler.Porcentaje(posiciones.Where(p => p.Dias > dias).Sum(p => p.Capital), posiciones.Sum(p => p.Capital));

    private Task<decimal> ColocacionAsync(AmbitoDeInformeDeCartera ambito, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var creditos = ambito.Creditos(db).Select(l => l.Id);
        return db.LoanOperations.AsNoTracking()
            .Where(o => creditos.Contains(o.LoanId) && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null
                        && o.ValueDate >= desde && o.ValueDate <= hasta)
            .SumAsync(o => o.Amount, ct);
    }

    private async Task<(decimal?, decimal?, string?)> DeterioroAsync(AmbitoDeInformeDeCartera ambito, DateOnly corte, CancellationToken ct)
    {
        var cierre = await CierresParaInformes.AlCorteAsync(db, corte, ct);
        if (cierre is null) return (null, null, "Todavía no hay un cierre de cartera vigente.");
        var actual = await QualityReportQueryHandler.CalidadAsync(db, ambito, cierre, ct);
        var anterior = await CierresParaInformes.AnteriorAsync(db, cierre, ct);
        decimal? previo = null;
        if (anterior is not null)
        {
            var c = await QualityReportQueryHandler.CalidadAsync(db, ambito, anterior, ct);
            previo = c.Lineas.Sum(l => l.TotalImpairment) + (c.General ?? []).Sum(g => g.Monto);
        }
        var nota = $"De {CierresParaInformes.Nombre(cierre)}" + (actual.General is null ? "; sólo el individual (el general no se reparte por línea)." : ".");
        return (actual.Lineas.Sum(l => l.TotalImpairment) + (actual.General ?? []).Sum(g => g.Monto), previo, nota);
    }

    private async Task<decimal?> RecaudoSobreEsperadoAsync(AmbitoDeInformeDeCartera ambito, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var creditos = ambito.Creditos(db).Select(l => l.Id);
        var recaudo = await db.LoanOperations.AsNoTracking()
            .Where(o => creditos.Contains(o.LoanId) && o.Kind == LoanOperationKind.Payment && o.ReversedByOperationId == null
                        && o.ValueDate >= desde && o.ValueDate <= hasta)
            .SumAsync(o => o.Amount, ct);

        var cuotas = await (from i in db.RepaymentInstallments.AsNoTracking()
                            join v in db.RepaymentScheduleVersions.AsNoTracking() on i.ScheduleVersionId equals v.Id
                            where creditos.Contains(v.LoanId) && i.DueDate >= desde && i.DueDate <= hasta
                            select new { v.LoanId, v.VersionNumber, i.InstallmentNumber, i.Total }).ToListAsync(ct);
        var conCuotas = cuotas.Select(c => c.LoanId).Distinct().ToList();
        var versiones = (await db.RepaymentScheduleVersions.AsNoTracking().Where(v => conCuotas.Contains(v.LoanId))
                .Select(v => new { v.LoanId, v.VersionNumber, v.FirstRegeneratedInstallment }).ToListAsync(ct))
            .ToLookup(v => v.LoanId);
        // Una cuota es la vigente si ninguna versión posterior del plan la regeneró.
        var esperado = cuotas.Where(c => !versiones[c.LoanId].Any(v => v.VersionNumber > c.VersionNumber && v.FirstRegeneratedInstallment <= c.InstallmentNumber))
            .Sum(c => c.Total);
        return QualityReportQueryHandler.Porcentaje(recaudo, esperado);
    }

    private async Task<decimal?> CosechaAsync(AmbitoDeInformeDeCartera ambito, DateOnly corte, CancellationToken ct)
    {
        var desde = new DateOnly(corte.Year, corte.Month, 1).AddMonths(1 - MesesDeLaCosecha);
        var cosecha = (await VintagesReportQueryHandler.CreditosAsync(db, ambito, corte, ct)).Where(c => c.PrimerDesembolso >= desde).ToList();
        if (cosecha.Count == 0) return null;
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, cosecha.Select(c => c.LoanId).ToList(), corte, ct);
        var enMora = posiciones.Values.Where(p => p.DaysPastDue >= MoraDeLaCosecha && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode))
            .Sum(p => p.Principal);
        return QualityReportQueryHandler.Porcentaje(enMora, cosecha.Sum(c => c.Monto));
    }

    private async Task<decimal?> PromesasAsync(AmbitoDeInformeDeCartera ambito, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var creditos = ambito.Creditos(db).Select(l => l.Id);
        var promesas = await (from p in db.PaymentPromises.AsNoTracking()
                              join a in db.CollectionActions.AsNoTracking() on p.CollectionActionId equals a.Id
                              where !p.IsDeleted && creditos.Contains(p.LoanId) && p.PromisedDate >= desde && p.PromisedDate <= hasta
                              select new { Promesa = p, a.Id, a.OccurredAt }).ToListAsync(ct);
        if (promesas.Count == 0) return null;
        var gestiones = promesas.Select(p => p.Id).ToList();
        var anuladas = (await db.CollectionActions.AsNoTracking().Where(a => a.VoidsActionId != null && gestiones.Contains(a.VoidsActionId.Value))
            .Select(a => a.VoidsActionId!.Value).ToListAsync(ct)).ToHashSet();
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, promesas.Select(p => p.Promesa.LoanId).Distinct().ToList(), ct);
        var estados = promesas.Where(p => !anuladas.Contains(p.Id))
            .Select(p => EstadoDePromesa.Calcular(p.Promesa.ParaEstado(p.OccurredAt, false), pagos.GetValueOrDefault(p.Promesa.LoanId) ?? [], hasta).Estado)
            .ToList();
        var cumplidas = estados.Count(e => e == PaymentPromiseStatus.Kept);
        var resueltas = cumplidas + estados.Count(e => e == PaymentPromiseStatus.Broken);
        return resueltas == 0 ? null : QualityReportQueryHandler.Porcentaje(cumplidas, resueltas);
    }

    private async Task<(decimal? Total, int Rechazados)> MensajesAsync(CancellationToken ct)
    {
        var vista = vistasDeBandeja.FirstOrDefault(v => string.Equals(v.Modulo, ModulosDeOrigen.Cartera, StringComparison.Ordinal));
        if (vista is null) return (null, 0);
        var conteos = await (await vista.VisiblesAsync(ct))
            .Where(d => d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.InBatch || d.Status == DeliveryStatus.Rejected
                        || d.Status == DeliveryStatus.ValidationFailed)
            .GroupBy(d => d.Status).Select(g => new { g.Key, Cantidad = g.Count() }).ToListAsync(ct);
        var rechazados = conteos.Where(c => c.Key is DeliveryStatus.Rejected or DeliveryStatus.ValidationFailed).Sum(c => c.Cantidad);
        return (conteos.Sum(c => c.Cantidad), rechazados);
    }

    private async Task<decimal?> AlertasAsync(CancellationToken ct)
    {
        if (alertas is null) return null;
        return await (await alertas.VisiblesAsync(ct)).AsNoTracking()
            .CountAsync(a => a.Module == ModuloDeAuditoria.Lending && a.Status == AlertStatus.Pending, ct);
    }

    private async Task<LendingDashboardScopeDto> AlcanceAsync(AlcanceDeCartera alcance, CancellationToken ct)
    {
        var oficinas = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).PorOficina(alcance, b => b.Id)
            .OrderBy(b => b.Name).Select(b => new LendingDashboardScopeItemDto(b.PublicId, b.LegacyCode ?? string.Empty, b.Name)).ToListAsync(ct);
        var lineas = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted).PorLinea(alcance, l => l.Id)
            .OrderBy(l => l.Code).Select(l => new LendingDashboardScopeItemDto(l.PublicId, l.Code, l.Name)).ToListAsync(ct);
        return new LendingDashboardScopeDto(oficinas, lineas);
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd");

    private static LendingDashboardLinkDto Informe(string vista, params (string Clave, string Valor)[] query)
    {
        var q = new Dictionary<string, string> { ["vista"] = vista };
        foreach (var (clave, valor) in query) q[clave] = valor;
        return new LendingDashboardLinkDto(PaginaDeInformes, q);
    }
}
