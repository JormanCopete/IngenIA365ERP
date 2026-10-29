using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reports;

/// <summary>Una vista de T0 de Tesorería con sus filtros (<c>GET /api/reports/treasury/{vista}</c>, <c>Treasury.Reports.View</c>). (nuevo)</summary>
public sealed record TreasuryReportQuery(string Vista, FiltrosDeInformeDeRecaudoYTesoreria Filtros) : IRequest<Result<TablaExportable>>;

/// <summary>
/// Las vistas de T0 de Tesorería (feature 015, US21, T0314; FR-153 parte T0): siempre sólo las cuentas propias al alcance (y la del filtro <c>account</c>). Una
/// orden que todavía no entró a un lote no tiene cuenta: se muestra a quien ve órdenes (la bandeja), como en la pantalla de órdenes. <c>recent-account-changes</c>
/// sigue en su consulta de la fase 9 y aquí sólo se reenvía. Los saldos en libros los pregunta a Contabilidad (<see cref="IContabilidadParaTesoreria"/>), nunca
/// leyendo cuentas, y su lectura queda en la auditoría como la de la pantalla (<c>Treasury.BankBalances.Read</c>). (nuevo)
/// </summary>
public sealed partial class TreasuryReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IContabilidadParaTesoreria contabilidad,
    AuditoriaDeTesoreria auditoria,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<TreasuryReportQuery, Result<TablaExportable>>
{
    public const string CodigoVistaDesconocida = "Treasury.Report.UnknownView";

    public async Task<Result<TablaExportable>> Handle(TreasuryReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var (desde, hasta, corte) = (f.Desde(hoy), f.Hasta(hoy), f.ALaFecha(hoy));
        var mio = await alcance.ObtenerAsync(ct);
        int? filtro = f.Account is { } a ? await db.OwnBankAccounts.AsNoTracking().Where(c => c.PublicId == a).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct) ?? -1 : null;
        bool Ve(int cuenta) => mio.IncluyeCuenta(cuenta) && (filtro is null || filtro == cuenta);
        var cuentas = await db.OwnBankAccounts.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        return request.Vista switch
        {
            VistasDeTesoreria.OrdenesPorEstado => Result.Success(await OrdenesAsync(Ve, filtro, cuentas, corte, ct)),
            VistasDeTesoreria.Lotes => Result.Success(await LotesAsync(Ve, cuentas, desde, hasta, ct)),
            VistasDeTesoreria.RechazadasYRegiro => Result.Success(await RechazadasAsync(Ve, filtro, cuentas, desde, hasta, ct)),
            VistasDeTesoreria.CambiosRecientesDeCuenta => await sender.Send(new RecentAccountChangesQuery(desde, hasta), ct),
            VistasDeTesoreria.ChequesGirados => Result.Success(await ChequesAsync(Ve, cuentas, desde, hasta, corte, ct)),
            VistasDeTesoreria.SaldosBancarios => Result.Success(await SaldosAsync(Ve, cuentas, corte, ct)),
            VistasDeTesoreria.ConciliacionMinima => Result.Success(await ConciliacionesAsync(Ve, cuentas, desde, hasta, ct)),
            // T1 (fase 35, T0423; FR-153 parte T1): los comprobantes de pago, los traslados y el cierre del mes de los cheques.
            VistasDeTesoreria.ComprobantesDePago => Result.Success(await ComprobantesDePagoAsync(Ve, cuentas, desde, hasta, ct)),
            VistasDeTesoreria.Traslados => Result.Success(await TrasladosAsync(Ve, cuentas, desde, hasta, ct)),
            VistasDeTesoreria.CierreDeCheques => Result.Success(await CierresDeChequesAsync(Ve, cuentas, desde, hasta, ct)),
            // T2 (fase 45, T0465; FR-153 parte T2): las conciliaciones, la posición de liquidez y el flujo de caja.
            VistasDeTesoreria.Conciliaciones => Result.Success(await ConciliacionesT2Async(Ve, cuentas, desde, hasta, ct)),
            VistasDeTesoreria.Liquidez => Result.Success(await LiquidezAsync(mio, desde, hasta, ct)),
            VistasDeTesoreria.FlujoDeCaja => Result.Success(await FlujoDeCajaAsync(mio, desde, hasta, ct)),
            _ => Result.Failure<TablaExportable>(new Error(CodigoVistaDesconocida, $"«{request.Vista}» no es una vista de Tesorería.")),
        };
    }

    private static string Periodo(DateOnly desde, DateOnly hasta) => $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

    private static string Cuenta(Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas, int id) =>
        cuentas.TryGetValue(id, out var c) ? $"{c.Code} · {c.Name}" : string.Empty;

    private async Task<TablaExportable> OrdenesAsync(Func<int, bool> ve, int? filtro, Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas,
        DateOnly corte, CancellationToken ct)
    {
        var vivas = new[] { PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready, PaymentOrderState.InBatch, PaymentOrderState.Approved, PaymentOrderState.Sent };
        var ordenes = await (from o in db.PaymentOrders.AsNoTracking()
                             join i in db.PaymentBatchItems.AsNoTracking().Where(x => x.IsLive) on o.Id equals i.PaymentOrderId into its
                             from i in its.DefaultIfEmpty()
                             join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id into bs
                             from b in bs.DefaultIfEmpty()
                             where vivas.Contains(o.Status)
                             orderby o.ReceivedAt
                             select new
                             {
                                 o.PublicId, o.Number, o.Origin, o.OrderKind, o.Form, o.BeneficiaryName, o.BeneficiaryTaxId, o.NetAmount, o.PayDate, o.Status, o.ReceivedAt,
                                 Cuenta = b != null ? (int?)b.BankAccountId : null, Lote = b != null ? b.Number : null,
                             }).ToListAsync(ct);
        // Sin lote no hay cuenta: la orden está en la bandeja y se muestra salvo que se pida una cuenta.
        ordenes = ordenes.Where(o => o.Cuenta is int c ? ve(c) : filtro is null).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Estado"), new("Orden"), new("Origen"), new("Clase"), new("Forma"), new("Beneficiario"), new("Documento"), new("Valor", TipoDeColumna.Moneda),
            new("Fecha de pago", TipoDeColumna.Fecha), new("Llegó"), new("Días", TipoDeColumna.Entero), new("Lote"), new("Cuenta de origen"), new("_orden", Clave: "_orden"),
        };
        var filas = ordenes.OrderBy(o => o.Status).ThenBy(o => o.ReceivedAt).Select(o =>
        {
            var dias = corte.DayNumber - DateOnly.FromDateTime(o.ReceivedAt).DayNumber;
            return new FilaExportable(
            [
                o.Status.ToString(), o.Number, o.Origin.ToString(), o.OrderKind, o.Form.ToString(), o.BeneficiaryName, o.BeneficiaryTaxId, o.NetAmount, o.PayDate,
                o.ReceivedAt.ToString("yyyy-MM-dd"), dias, o.Lote, o.Cuenta is int c ? Cuenta(cuentas, c) : null, o.PublicId,
            ], Seccion: o.Status.ToString(), Resaltada: o.Status is PaymentOrderState.Pending);
        }).ToList();
        return new TablaExportable("Órdenes de pago por estado", $"Vivas al {corte:dd/MM/yyyy}", columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, null, ordenes.Sum(o => o.NetAmount), null, null, null, null, null, null]), []);
    }

    private async Task<TablaExportable> LotesAsync(Func<int, bool> ve, Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas, DateOnly desde,
        DateOnly hasta, CancellationToken ct)
    {
        var lotes = await db.PaymentBatches.AsNoTracking().Where(b => b.PayDate >= desde && b.PayDate <= hasta).OrderBy(b => b.PayDate).ThenBy(b => b.Number).ToListAsync(ct);
        lotes = lotes.Where(b => ve(b.BankAccountId)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Lote"), new("Cuenta de origen"), new("Forma"), new("Fecha de pago", TipoDeColumna.Fecha), new("Órdenes", TipoDeColumna.Entero), new("Total", TipoDeColumna.Moneda),
            new("GMF estimado", TipoDeColumna.Moneda), new("Estado"), new("Preparado"), new("Aprobado"), new("Enviado"), new("_lote", Clave: "_lote"),
        };
        var filas = lotes.Select(b => new FilaExportable(
        [
            b.Number, Cuenta(cuentas, b.BankAccountId), b.Form.ToString(), b.PayDate, b.OrderCount, b.Total, b.GmfEstimate, b.Status.ToString(),
            b.PreparedAt.ToString("yyyy-MM-dd HH:mm"), b.ApprovedAt?.ToString("yyyy-MM-dd HH:mm"), b.SentAt?.ToString("yyyy-MM-dd HH:mm"), b.PublicId,
        ], Resaltada: b.Status is PaymentBatchState.RejectedByBank or PaymentBatchState.PartiallyConfirmed)).ToList();
        return new TablaExportable("Lotes de pago", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, null, lotes.Sum(b => b.OrderCount), lotes.Sum(b => b.Total), lotes.Sum(b => b.GmfEstimate), null, null, null, null, null]), []);
    }

    private async Task<TablaExportable> RechazadasAsync(Func<int, bool> ve, int? filtro, Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas,
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var malas = new[] { DisbursementOutcome.Rejected, DisbursementOutcome.CheckNotCashed, DisbursementOutcome.Voided };
        var resultados = await (from r in db.DisbursementResults.AsNoTracking()
                                join o in db.PaymentOrders.AsNoTracking() on r.PaymentOrderId equals o.Id
                                where malas.Contains(r.Outcome) && r.OccurredOn >= desde && r.OccurredOn <= hasta
                                orderby r.OccurredOn
                                select new { o.Id, o.PublicId, o.Number, o.Origin, o.BeneficiaryName, o.NetAmount, r.Outcome, r.OccurredOn, r.Reason, r.RecordedByName })
            .ToListAsync(ct);
        var ids = resultados.Select(r => r.Id).ToList();
        var cuentaDe = await (from i in db.PaymentBatchItems.AsNoTracking()
                              join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                              where ids.Contains(i.PaymentOrderId)
                              select new { i.PaymentOrderId, b.BankAccountId, i.Id }).ToListAsync(ct);
        var cuentaPorOrden = cuentaDe.GroupBy(x => x.PaymentOrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().BankAccountId);
        var reemplazos = await db.PaymentOrders.AsNoTracking().Where(o => o.ReplacesOrderId != null && ids.Contains(o.ReplacesOrderId.Value))
            .Select(o => new { Reemplaza = o.ReplacesOrderId!.Value, o.Number, o.Status }).ToListAsync(ct);
        resultados = resultados.Where(r => cuentaPorOrden.TryGetValue(r.Id, out var c) ? ve(c) : filtro is null).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Fecha", TipoDeColumna.Fecha), new("Resultado"), new("Orden"), new("Origen"), new("Beneficiario"), new("Valor", TipoDeColumna.Moneda), new("Motivo"),
            new("Registró"), new("Cuenta de origen"), new("Orden de regiro"), new("Estado del regiro"), new("_orden", Clave: "_orden"),
        };
        var filas = resultados.Select(r =>
        {
            var nueva = reemplazos.FirstOrDefault(x => x.Reemplaza == r.Id);
            return new FilaExportable(
            [
                r.OccurredOn, r.Outcome.ToString(), r.Number, r.Origin.ToString(), r.BeneficiaryName, r.NetAmount, r.Reason, r.RecordedByName,
                cuentaPorOrden.TryGetValue(r.Id, out var c) ? Cuenta(cuentas, c) : null, nueva?.Number, nueva?.Status.ToString(), r.PublicId,
            ], Resaltada: nueva is null);
        }).ToList();
        return new TablaExportable("Giros rechazados y regiro", Periodo(desde, hasta), columnas, filas, null,
            ["Resaltadas las que todavía no tienen una orden que las reemplace: el regiro lo decide el módulo de origen."]);
    }

    private async Task<TablaExportable> ChequesAsync(Func<int, bool> ve, Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas, DateOnly desde,
        DateOnly hasta, DateOnly corte, CancellationToken ct)
    {
        var cheques = await (from c in db.IssuedChecks.AsNoTracking()
                             join k in db.Checkbooks.AsNoTracking() on c.CheckbookId equals k.Id
                             where c.IssuedOn >= desde && c.IssuedOn <= hasta
                             orderby k.BankAccountId, c.CheckNumber
                             select new { c.PublicId, k.BankAccountId, c.CheckNumber, c.BeneficiaryPersonId, c.Amount, c.IssuedOn, c.Status, c.DeliveredOn, c.CashedOn, c.VoidReason })
            .ToListAsync(ct);
        cheques = cheques.Where(c => ve(c.BankAccountId)).ToList();
        var ids = cheques.Where(c => c.BeneficiaryPersonId != null).Select(c => c.BeneficiaryPersonId!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => (p.BusinessName ?? (p.FirstName + " " + p.LastName)).Trim(), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Cuenta"), new("Cheque", TipoDeColumna.Entero), new("Beneficiario"), new("Valor", TipoDeColumna.Moneda), new("Emitido", TipoDeColumna.Fecha), new("Estado"),
            new("Entregado"), new("Cobrado"), new("Días sin cobrar", TipoDeColumna.Entero), new("Motivo de anulación"), new("_cheque", Clave: "_cheque"),
        };
        var filas = cheques.Select(c =>
        {
            int? dias = c.Status is IssuedCheckState.Issued or IssuedCheckState.Delivered ? corte.DayNumber - (c.DeliveredOn ?? c.IssuedOn).DayNumber : null;
            return new FilaExportable(
            [
                Cuenta(cuentas, c.BankAccountId), c.CheckNumber, c.BeneficiaryPersonId is int b ? personas.GetValueOrDefault(b) : null, c.Amount, c.IssuedOn, c.Status.ToString(),
                c.DeliveredOn?.ToString("yyyy-MM-dd"), c.CashedOn?.ToString("yyyy-MM-dd"), dias, c.VoidReason, c.PublicId,
            ], Resaltada: dias is not null);
        }).ToList();
        return new TablaExportable("Cheques girados", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, cheques.Sum(c => c.Amount), null, null, null, null, null, null, null]), []);
    }

    private async Task<TablaExportable> SaldosAsync(Func<int, bool> ve, Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas, DateOnly corte,
        CancellationToken ct)
    {
        var visibles = cuentas.Values.Where(c => ve(c.Id) && c.ValidFrom <= corte).OrderBy(c => c.Code).ToList();
        var bancos = await db.Banks.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Cuenta"), new("Banco"), new("Número"), new("Estado"), new("Saldo en libros", TipoDeColumna.Moneda), new("Último extracto"),
            new("Saldo del extracto", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda), new("_cuenta", Clave: "_cuenta"),
        };
        var filas = new List<FilaExportable>();
        decimal totalLibros = 0m;
        foreach (var c in visibles)
        {
            var libros = await contabilidad.SaldoEnLibrosAsync(c.PublicId, corte, ct);
            totalLibros += libros;
            decimal? diferencia = c.LastStatementBalance is { } e ? e - libros : null;
            filas.Add(new FilaExportable(
            [
                $"{c.Code} · {c.Name}", bancos.GetValueOrDefault(c.BankId), CuentasBancariasPropias.Enmascarar(c.AccountNumber), c.Status.ToString(), libros,
                c.LastStatementDate?.ToString("yyyy-MM-dd"), c.LastStatementBalance, diferencia, c.PublicId,
            ], Resaltada: diferencia is not null and not 0m));
        }
        if (visibles.Count > 0) await auditoria.SaldosBancariosLeidosAsync(visibles.Select(c => c.PublicId).ToList(), corte, ct);
        return new TablaExportable("Saldos bancarios", $"Al {corte:dd/MM/yyyy}", columnas, filas,
            new FilaExportable(["Total", null, null, null, totalLibros, null, null, null, null]),
            ["El saldo en libros lo responde Contabilidad por el vínculo de cada cuenta propia; el del extracto es el del último extracto cargado, que puede ser de otra fecha."]);
    }

    private async Task<TablaExportable> ConciliacionesAsync(Func<int, bool> ve, Dictionary<int, Domain.Entities.Treasury.BankAccounts.OwnBankAccount> cuentas, DateOnly desde,
        DateOnly hasta, CancellationToken ct)
    {
        var de = desde.Year * 100 + desde.Month;
        var a = hasta.Year * 100 + hasta.Month;
        var conciliaciones = await db.TreasuryReconciliations.AsNoTracking()
            .Where(r => r.PeriodYear * 100 + r.PeriodMonth >= de && r.PeriodYear * 100 + r.PeriodMonth <= a)
            .OrderBy(r => r.BankAccountId).ThenBy(r => r.PeriodYear).ThenBy(r => r.PeriodMonth).ThenBy(r => r.Version).ToListAsync(ct);
        conciliaciones = conciliaciones.Where(r => ve(r.BankAccountId)).ToList();
        var ids = conciliaciones.Select(r => r.Id).ToList();
        var partidas = await db.TreasuryReconcilingItems.AsNoTracking().Where(i => ids.Contains(i.ReconciliationId) && !i.IsVoided)
            .Select(i => new { i.ReconciliationId, i.Kind, i.Amount }).ToListAsync(ct);
        var clases = Enum.GetValues<ReconcilingItemKind>();
        var columnas = new List<ColumnaExportable>
        {
            new("Cuenta"), new("Período"), new("Versión", TipoDeColumna.Entero), new("Estado"), new("Saldo del extracto", TipoDeColumna.Moneda),
            new("Saldo en libros", TipoDeColumna.Moneda),
        };
        columnas.AddRange(clases.Select(k => new ColumnaExportable(k.ToString(), TipoDeColumna.Moneda)));
        columnas.AddRange([new("Diferencia", TipoDeColumna.Moneda), new("Cerrada"), new("_conciliacion", Clave: "_conciliacion")]);
        var filas = conciliaciones.Select(r =>
        {
            var valores = new List<object?>
            {
                Cuenta(cuentas, r.BankAccountId), $"{r.PeriodYear}-{r.PeriodMonth:00}", r.Version, r.Status.ToString(), r.StatementBalance, r.LedgerBalance,
            };
            valores.AddRange(clases.Select(k => (object?)partidas.Where(p => p.ReconciliationId == r.Id && p.Kind == k).Sum(p => p.Amount)));
            valores.AddRange([r.Difference, r.ClosedAt?.ToString("yyyy-MM-dd"), r.PublicId]);
            return new FilaExportable(valores, Resaltada: r.Difference != 0m || r.Status == ReconciliationState.Outdated);
        }).ToList();
        return new TablaExportable("Conciliación bancaria mínima", Periodo(desde, hasta), columnas, filas, null,
            ["Una conciliación desactualizada (Outdated) es la de un mes cuyo período contable se reabrió: se vuelve a conciliar con una versión nueva."]);
    }
}
