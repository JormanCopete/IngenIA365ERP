using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero.Caja;

// 2026-11-17 (pedido del dueño): las cifras de Caja (Recaudo) y Tesorería del tablero de inicio. El recaudo es el mismo del tablero de
// Recaudo (/recaudo/tablero): recibos no anulados por su fecha valor y su valor aplicado, en las oficinas del alcance de Recaudo del
// usuario. «Solo lo mío» = los recibos que registró el usuario como cajero.

/// <summary>Lo común del recaudo: los recibos del alcance, de un rango, y si se pide sólo los del cajero.</summary>
public sealed class RecaudoDelTablero(IApplicationDbContext db, IAlcanceDeRecaudo alcanceDeRecaudo)
{
    public const string Permiso = "Receipts.Reports.View";
    public const string Grupo = "Caja";
    public const string Tablero = "/recaudo/tablero";

    public async Task<IQueryable<Domain.Entities.Receipts.Operations.Receipt>?> RecibosAsync(
        ContextoDeIndicador contexto, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var alcance = await alcanceDeRecaudo.ObtenerAsync(ct);
        var recibos = db.IssuedReceipts.AsNoTracking()
            .Where(r => r.ValueDate >= desde && r.ValueDate <= hasta && r.Status != ReceiptState.Voided);
        if (!alcance.Total)
        {
            var oficinas = alcance.Oficinas.ToArray();
            recibos = recibos.Where(r => oficinas.Contains(r.BranchId));
        }
        if (contexto.SoloLoMio)
        {
            if (contexto.UsuarioId is not int usuario) return null;
            recibos = recibos.Where(r => r.CashierUserId == usuario);
        }
        return recibos;
    }

    public async Task<IReadOnlyDictionary<DateOnly, decimal>> PorDiaAsync(ContextoDeIndicador contexto, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recibos = await RecibosAsync(contexto, desde, hasta, ct);
        if (recibos is null) return new Dictionary<DateOnly, decimal>();
        return await recibos.GroupBy(r => r.ValueDate).Select(g => new { Dia = g.Key, Valor = g.Sum(r => r.TotalApplied) })
            .ToDictionaryAsync(x => x.Dia, x => x.Valor, ct);
    }

    /// <summary>Las sesiones de caja (de Recaudo) en estos estados, en las oficinas del alcance; con «Solo lo mío», las del cajero.</summary>
    public async Task<List<int>> SesionesAsync(ContextoDeIndicador contexto, IReadOnlyCollection<CashSessionState> estados, CancellationToken ct)
    {
        var alcance = await alcanceDeRecaudo.ObtenerAsync(ct);
        var filas = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                           join c in db.CashBoxes.AsNoTracking() on s.CashBoxId equals c.Id
                           join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                           where estados.Contains(s.Status)
                           select new { s.Id, s.CashierUserId, p.BranchId }).ToListAsync(ct);
        return filas.Where(s => alcance.IncluyeOficina(s.BranchId) && (!contexto.SoloLoMio || s.CashierUserId == contexto.UsuarioId))
            .Select(s => s.Id).ToList();
    }
}

/// <summary>Recaudo del día contra ayer.</summary>
public sealed class RecaudoDelDia(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IIndicadorDelTablero
{
    private readonly RecaudoDelTablero recaudo = new(db, alcance);

    public DefinicionDeIndicador Definicion { get; } = new(
        "caja.recaudo-del-dia", "Recaudo del día", RecaudoDelTablero.Grupo, RecaudoDelTablero.Permiso, AdmiteSoloLoMio: true, TipoDeIndicador.Cifra,
        FormatoDeIndicador.Moneda, RecaudoDelTablero.Tablero,
        "Valor aplicado de los recibos de hoy (caja, bancos y pagadurías), sin los anulados, comparado con ayer. «Solo lo mío»: los recibos que usted registró.",
        PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var hoy = contexto.Hoy;
        var porDia = await recaudo.PorDiaAsync(contexto, hoy.AddDays(-1), hoy, ct);
        return Result.Success(new ResultadoDeIndicador(porDia.GetValueOrDefault(hoy), porDia.GetValueOrDefault(hoy.AddDays(-1)), "ayer"));
    }
}

/// <summary>Recaudo del mes contra el mismo tramo del mes anterior.</summary>
public sealed class RecaudoDelMes(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IIndicadorDelTablero
{
    private readonly RecaudoDelTablero recaudo = new(db, alcance);

    public DefinicionDeIndicador Definicion { get; } = new(
        "caja.recaudo-del-mes", "Recaudo del mes", RecaudoDelTablero.Grupo, RecaudoDelTablero.Permiso, AdmiteSoloLoMio: true, TipoDeIndicador.Cifra,
        FormatoDeIndicador.Moneda, "/recaudo/informes",
        "Valor aplicado de los recibos del mes hasta hoy, comparado con el mes anterior hasta el mismo día.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var hoy = contexto.Hoy;
        var inicio = new DateOnly(hoy.Year, hoy.Month, 1);
        var esteMes = (await recaudo.PorDiaAsync(contexto, inicio, hoy, ct)).Values.Sum();
        var anterior = (await recaudo.PorDiaAsync(contexto, inicio.AddMonths(-1), hoy.AddMonths(-1), ct)).Values.Sum();
        return Result.Success(new ResultadoDeIndicador(esteMes, anterior, "mes anterior"));
    }
}

/// <summary>Lo recaudado hoy por medio de pago (efectivo, tarjeta, transferencia…), en barras.</summary>
public sealed class RecaudoPorMedioDePago(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IIndicadorDelTablero
{
    private readonly RecaudoDelTablero recaudo = new(db, alcance);

    public DefinicionDeIndicador Definicion { get; } = new(
        "caja.recaudo-por-medio", "Recaudo de hoy por medio de pago", RecaudoDelTablero.Grupo, RecaudoDelTablero.Permiso, AdmiteSoloLoMio: true,
        TipoDeIndicador.Serie, FormatoDeIndicador.Moneda, RecaudoDelTablero.Tablero,
        "Lo recibido hoy por cada medio de pago en los recibos no anulados; la cifra es el total.");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var recibos = await recaudo.RecibosAsync(contexto, contexto.Hoy, contexto.Hoy, ct);
        if (recibos is null) return Result.Success(new ResultadoDeIndicador(0m, Serie: []));
        var porMedio = await (from m in db.ReceiptMeans.AsNoTracking()
                              join r in recibos on m.ReceiptId equals r.Id
                              where m.Status == ReceiptMeansState.Active
                              group m.Amount by m.PaymentMeansId into g
                              select new { Medio = g.Key, Valor = g.Sum() }).ToListAsync(ct);
        var ids = porMedio.Select(x => x.Medio).ToList();
        var nombres = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var serie = porMedio.OrderByDescending(x => x.Valor)
            .Select(x => new PuntoDeSerie(nombres.GetValueOrDefault(x.Medio) ?? "Otro", x.Valor)).ToList();
        return Result.Success(new ResultadoDeIndicador(serie.Sum(p => p.Valor), Serie: serie));
    }
}

/// <summary>Sesiones de caja abiertas y lo recaudado en ellas. «Solo lo mío»: la caja del usuario.</summary>
public sealed class SesionesDeCajaAbiertas(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IIndicadorDelTablero
{
    private readonly RecaudoDelTablero recaudo = new(db, alcance);

    public DefinicionDeIndicador Definicion { get; } = new(
        "caja.sesiones-abiertas", "Cajas abiertas", RecaudoDelTablero.Grupo, "Receipts.Session.View", AdmiteSoloLoMio: true, TipoDeIndicador.Cifra,
        FormatoDeIndicador.Numero, "/recaudo/sesiones",
        "Sesiones de caja abiertas y lo recaudado en ellas. «Solo lo mío»: su propia caja.", PorDefecto: true);

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var sesiones = await recaudo.SesionesAsync(contexto, [CashSessionState.Open], ct);
        if (sesiones.Count == 0) return Result.Success(new ResultadoDeIndicador(0m, Nota: contexto.SoloLoMio ? "No tiene caja abierta" : "Ninguna caja abierta"));
        var recaudado = await db.IssuedReceipts.AsNoTracking()
            .Where(r => r.CashSessionId != null && sesiones.Contains(r.CashSessionId.Value) && r.Status != ReceiptState.Voided)
            .SumAsync(r => (decimal?)r.TotalApplied, ct) ?? 0m;
        var texto = recaudado.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        return Result.Success(new ResultadoDeIndicador(sesiones.Count, Nota: $"Recaudado en ellas: $ {texto}"));
    }
}

/// <summary>Órdenes de pago por girar: recibidas y todavía no enviadas al banco ni pagadas.</summary>
public sealed class PagosPorGirar(IApplicationDbContext db) : IIndicadorDelTablero
{
    private static readonly PaymentOrderState[] PorGirar =
    [
        PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready, PaymentOrderState.InBatch, PaymentOrderState.Approved,
    ];

    public DefinicionDeIndicador Definicion { get; } = new(
        "tesoreria.pagos-por-girar", "Pagos por girar", "Tesorería", "Treasury.Orders.View", AdmiteSoloLoMio: false, TipoDeIndicador.Cifra,
        FormatoDeIndicador.Moneda, "/tesoreria/ordenes",
        "Valor neto de las órdenes de pago que todavía no salen al banco (recibidas, pendientes, listas, en lote o aprobadas).");

    public async Task<Result<ResultadoDeIndicador>> CalcularAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var ordenes = await db.PaymentOrders.AsNoTracking().Where(o => PorGirar.Contains(o.Status))
            .GroupBy(_ => 1).Select(g => new { Cantidad = g.Count(), Valor = g.Sum(o => o.NetAmount) }).FirstOrDefaultAsync(ct);
        return Result.Success(new ResultadoDeIndicador(ordenes?.Valor ?? 0m, Nota: $"{ordenes?.Cantidad ?? 0} órdenes"));
    }
}

// ----------------------------------------------------------------------------------------------------- pendientes --

/// <summary>Sesiones olvidadas (abiertas después de la hora de corte) que el supervisor debe cerrar.</summary>
public sealed class SesionesOlvidadasPorCerrar(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IPendienteDelTablero
{
    private readonly RecaudoDelTablero recaudo = new(db, alcance);

    public DefinicionDePendiente Definicion { get; } = new(
        "caja.sesiones-olvidadas", "Cajas olvidadas por cerrar", RecaudoDelTablero.Grupo, "Receipts.Session.CloseForgotten", "/recaudo/sesiones");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        (await recaudo.SesionesAsync(contexto, [CashSessionState.ForgottenOpen], ct)).Count;
}

/// <summary>Sesiones cerradas con diferencia de arqueo que esperan aprobación.</summary>
public sealed class DiferenciasDeArqueoPorAprobar(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IPendienteDelTablero
{
    private readonly RecaudoDelTablero recaudo = new(db, alcance);

    public DefinicionDePendiente Definicion { get; } = new(
        "caja.diferencias-por-aprobar", "Diferencias de arqueo por aprobar", RecaudoDelTablero.Grupo, "Receipts.Count.ApproveDifference", "/recaudo/sesiones");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        (await recaudo.SesionesAsync(contexto, [CashSessionState.ClosedWithDifferencePending], ct)).Count;
}

/// <summary>Anulaciones de recibos pedidas que esperan aprobación, de las oficinas del alcance.</summary>
public sealed class AnulacionesDeRecibosPorAprobar(IApplicationDbContext db, IAlcanceDeRecaudo alcanceDeRecaudo) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "caja.anulaciones-por-aprobar", "Anulaciones de recibos por aprobar", RecaudoDelTablero.Grupo, "Receipts.Voiding.Approve", "/recaudo/anulaciones");

    public async Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct)
    {
        var alcance = await alcanceDeRecaudo.ObtenerAsync(ct);
        var oficinas = await (from v in db.ReceiptVoidings.AsNoTracking()
                              join r in db.IssuedReceipts.AsNoTracking() on v.ReceiptId equals r.Id
                              where v.Status == ReceiptVoidingState.PendingApproval
                              select r.BranchId).ToListAsync(ct);
        return oficinas.Count(alcance.IncluyeOficina);
    }
}

/// <summary>Lotes de pago que esperan aprobación.</summary>
public sealed class LotesDePagoPorAprobar(IApplicationDbContext db) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "tesoreria.lotes-por-aprobar", "Lotes de pago por aprobar", "Tesorería", "Treasury.Batches.Approve", "/tesoreria/lotes");

    public Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        db.PaymentBatches.AsNoTracking().CountAsync(b => b.Status == PaymentBatchState.PendingApproval, ct);
}

/// <summary>Cuentas bancarias de personas que esperan aprobación (la lista está en Maestros › Cuentas bancarias por aprobar).</summary>
public sealed class CuentasBancariasPorAprobar(IApplicationDbContext db) : IPendienteDelTablero
{
    public DefinicionDePendiente Definicion { get; } = new(
        "personas.cuentas-bancarias-por-aprobar", "Cuentas bancarias por aprobar", "Maestros", "Core.PersonBankAccounts.Approve",
        "/maestros/cuentas-bancarias-por-aprobar");

    public Task<int> ContarAsync(ContextoDeIndicador contexto, CancellationToken ct) =>
        db.PersonBankAccounts.AsNoTracking().CountAsync(c => !c.IsDeleted && c.Status == PersonBankAccountState.PendingApproval, ct);
}
