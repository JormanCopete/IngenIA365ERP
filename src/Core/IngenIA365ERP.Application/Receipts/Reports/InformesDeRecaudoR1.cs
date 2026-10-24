using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Reports;

// Feature 015, R1, US21 (T0401; FR-081, FR-087, FR-152 parte R1; api.md §17): las vistas de R1 del centro de informes de Recaudo. El consolidado suma, de sólo lectura,
// lo descontado por Nómina (IRecaudoDeNominaParaConsolidado) y el débito automático de Cartera (IDebitoAutomaticoParaConsolidado), que no son de una oficina: sus
// columnas son de toda la cooperativa y la nota lo dice.
public sealed partial class ReceiptsReportQueryHandler
{
    private async Task<TablaExportable> ConsolidadoAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recibos = await db.IssuedReceipts.AsNoTracking().Where(r => r.Status != ReceiptState.Voided && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new { r.ValueDate, r.CashBoxId, r.BranchId, r.Channel, r.TotalApplied }).ToListAsync(ct);
        recibos = recibos.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var deNomina = nomina is null ? [] : await nomina.PorDiaAsync(desde, hasta, ct);
        var debitos = debitoAutomatico is null ? [] : await debitoAutomatico.PorDiaAsync(desde, hasta, ct);
        var dias = recibos.Select(r => r.ValueDate).Concat(deNomina.Select(n => n.PayDate)).Concat(debitos.Select(d => d.Date)).Distinct().Order().ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Día", TipoDeColumna.Fecha), new("Caja", TipoDeColumna.Moneda), new("Bancos", TipoDeColumna.Moneda), new("Pagadurías", TipoDeColumna.Moneda),
            new("Otros canales", TipoDeColumna.Moneda), new("Nómina", TipoDeColumna.Moneda), new("Débito automático", TipoDeColumna.Moneda), new("Total", TipoDeColumna.Moneda),
        };
        decimal Canal(DateOnly d, params ReceiptChannel[] canales) => recibos.Where(r => r.ValueDate == d && canales.Contains(r.Channel)).Sum(r => r.TotalApplied);
        var filas = dias.Select(d =>
        {
            var caja = Canal(d, ReceiptChannel.CashDesk);
            var bancos = Canal(d, ReceiptChannel.Bank);
            var pagadurias = Canal(d, ReceiptChannel.Paymaster);
            var otros = recibos.Where(r => r.ValueDate == d && r.Channel is not (ReceiptChannel.CashDesk or ReceiptChannel.Bank or ReceiptChannel.Paymaster)).Sum(r => r.TotalApplied);
            var n = deNomina.Where(x => x.PayDate == d).Sum(x => x.Loans + x.Accounts);
            var da = debitos.Where(x => x.Date == d).Sum(x => x.Amount);
            return new FilaExportable([d, caja, bancos, pagadurias, otros, n, da, caja + bancos + pagadurias + otros + n + da]);
        }).ToList();
        var totales = new FilaExportable(["Total", .. Enumerable.Range(1, 7).Select(i => (object?)filas.Sum(f => (decimal)f.Valores[i]!))]);
        return new TablaExportable("Recaudo consolidado", Periodo(desde, hasta), columnas, filas, totales,
            ["Nómina y débito automático son de toda la cooperativa y de sólo lectura: los recaudan Nómina y Cartera por sus contratos."]);
    }

    private async Task<TablaExportable> AnulacionesConReemisionAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var anulados = await db.IssuedReceipts.AsNoTracking().Where(r => r.Status == ReceiptState.Voided && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new { r.Id, r.PublicId, r.Number, r.ValueDate, r.CashBoxId, r.BranchId, r.PayerPersonId, r.TotalApplied }).ToListAsync(ct);
        anulados = anulados.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var ids = anulados.Select(r => r.Id).ToList();
        var anulaciones = await db.ReceiptVoidings.AsNoTracking().Where(v => ids.Contains(v.ReceiptId) && v.Status == ReceiptVoidingState.Applied)
            .ToDictionaryAsync(v => v.ReceiptId, ct);
        var pagadores = anulados.Select(r => r.PayerPersonId).Distinct().ToList();
        var vigentes = await db.IssuedReceipts.AsNoTracking().Where(r => r.Status != ReceiptState.Voided && pagadores.Contains(r.PayerPersonId) && r.ValueDate >= desde)
            .Select(r => new { r.PayerPersonId, r.Number, r.ValueDate, r.RegisteredAt, r.TotalApplied }).ToListAsync(ct);
        var personas = await PersonasAsync(pagadores, ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Recibo anulado"), new("Fecha", TipoDeColumna.Fecha), new("Pagador"), new("Valor", TipoDeColumna.Moneda), new("Motivo"), new("Recibo que lo reemplaza"),
            new("Valor reemitido", TipoDeColumna.Moneda), new("_recibo", Clave: "_recibo"),
        };
        var filas = anulados.OrderBy(r => r.ValueDate).Select(r =>
        {
            var anulacion = anulaciones.GetValueOrDefault(r.Id);
            var desdeLaAnulacion = anulacion?.DecidedAt ?? anulacion?.RequestedAt;
            var reemplazo = vigentes.Where(v => v.PayerPersonId == r.PayerPersonId && (desdeLaAnulacion is null || v.RegisteredAt >= desdeLaAnulacion))
                .OrderBy(v => v.RegisteredAt).FirstOrDefault();
            return new FilaExportable([r.Number, r.ValueDate, personas.GetValueOrDefault(r.PayerPersonId).Nombre, r.TotalApplied, anulacion?.Reason, reemplazo?.Number,
                reemplazo?.TotalApplied, r.PublicId], Resaltada: reemplazo is null);
        }).ToList();
        return new TablaExportable("Anulaciones con reemisión", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, anulados.Sum(r => r.TotalApplied), null, null, filas.Sum(f => (decimal?)f.Valores[6] ?? 0m), null]),
            ["El recibo que reemplaza es el primero vigente del mismo pagador registrado desde la anulación; sin reemplazo, la fila va resaltada."]);
    }

    private async Task<TablaExportable> PagaduriasMorosasAsync(Ambito a, DateOnly corte, CancellationToken ct)
    {
        var filas = (await Paymasters.CalendarioDePagadurias.FilasAsync(db, a.Alcance, null, null, corte, ct)).Where(f => f.IsLate).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Pagaduría"), new("Período"), new("Descontado", TipoDeColumna.Moneda), new("Girado", TipoDeColumna.Moneda), new("Pendiente", TipoDeColumna.Moneda),
            new("Pago de la nómina", TipoDeColumna.Fecha), new("Fecha legal de giro", TipoDeColumna.Fecha), new("Días de mora", TipoDeColumna.Entero),
            new("_pagaduria", Clave: "_pagaduria"),
        };
        var lista = filas.Select(f => new FilaExportable([f.PaymasterName, $"{f.Year}-{f.Month:00}{(f.SubPeriod > 0 ? $"/{f.SubPeriod}" : string.Empty)}", f.Deducted, f.Remitted,
            f.Pending, f.PayrollPayDate, f.LegalRemittanceDate, f.LegalRemittanceDate is { } l ? corte.DayNumber - l.DayNumber : 0, f.PaymasterPersonPublicId],
            Resaltada: true)).ToList();
        return new TablaExportable("Pagadurías morosas", $"Al {corte:dd/MM/yyyy}", columnas, lista,
            new FilaExportable(["Total", null, filas.Sum(f => f.Deducted), filas.Sum(f => f.Remitted), filas.Sum(f => f.Pending), null, null, null, null]),
            ["Informativo (Ley 1527 art. 6): el plazo no bloquea nada; soporta el cobro solidario."]);
    }

    private async Task<TablaExportable> ChequesRecibidosAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var cheques = await (from c in db.ReceivedChecks.AsNoTracking()
                             join b in db.Banks.AsNoTracking().IgnoreQueryFilters() on c.DraweeBankId equals b.Id
                             where c.CreatedAt >= desde.ToDateTime(TimeOnly.MinValue) && c.CreatedAt < hasta.AddDays(1).ToDateTime(TimeOnly.MinValue)
                             orderby c.Status, c.PresentationDeadline
                             join l in db.ClearingBatches.AsNoTracking() on c.ClearingBatchId equals l.Id into ls
                             from l in ls.DefaultIfEmpty()
                             select new { c.PublicId, Banco = b.Name, c.CheckNumber, c.PayerPersonId, c.DrawerName, c.IssueDate, c.Amount, c.IsThirdParty, c.PresentationDeadline,
                                 c.BranchId, c.Status, c.ReturnCauseCode, Lote = l != null ? l.Number : null }).ToListAsync(ct);
        cheques = cheques.Where(c => a.Alcance.IncluyeOficina(c.BranchId) && (a.Oficina is null || a.Oficina == c.BranchId)).ToList();
        var personas = await PersonasAsync(cheques.Select(c => c.PayerPersonId), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Estado"), new("Banco"), new("Cheque"), new("Pagador"), new("Girador"), new("De tercero"), new("Fecha del cheque", TipoDeColumna.Fecha),
            new("Presentar antes de", TipoDeColumna.Fecha), new("Valor", TipoDeColumna.Moneda), new("Causal de devolución"),
            // R2 (fase 37; FR-084, FR-087): el lote de canje en que va el cheque.
            new("Lote de canje"), new("_cheque", Clave: "_cheque"),
        };
        var filas = cheques.Select(c => new FilaExportable([Estado(c.Status), c.Banco, c.CheckNumber, personas.GetValueOrDefault(c.PayerPersonId).Nombre, c.DrawerName,
            c.IsThirdParty ? "Sí" : "No", c.IssueDate, c.PresentationDeadline, c.Amount, c.ReturnCauseCode, c.Lote, c.PublicId], Seccion: Estado(c.Status),
            Resaltada: c.Status == ReceivedCheckState.Returned)).ToList();
        return new TablaExportable("Cheques recibidos", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, null, null, cheques.Sum(c => c.Amount), null, null, null]),
            ["Desde R2 los cheques consignados van en un lote de canje y quedan pagados al cruzar el abono del extracto que los paga (FR-084)."]);

        static string Estado(ReceivedCheckState s) => s switch
        {
            ReceivedCheckState.Received => "Recibido",
            ReceivedCheckState.InCustody => "En custodia",
            ReceivedCheckState.Deposited => "Consignado",
            ReceivedCheckState.InClearing => "En canje",
            ReceivedCheckState.Paid => "Pagado",
            ReceivedCheckState.Returned => "Devuelto",
            _ => s.ToString(),
        };
    }

    private async Task<TablaExportable> TransaccionesEnEfectivoAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas0 = await db.ReceiptsCashTransactions.AsNoTracking().Where(t => t.OccurredOn >= desde && t.OccurredOn <= hasta)
            .OrderBy(t => t.OccurredOn).ThenBy(t => t.Id)
            .Select(t => new { t.DocumentNumber, t.DocumentKind, t.Direction, t.PersonId, t.CashAmount, t.OtherMeansAmount, t.BranchId, t.OccurredOn }).ToListAsync(ct);
        filas0 = filas0.Where(t => a.Alcance.IncluyeOficina(t.BranchId) && (a.Oficina is null || a.Oficina == t.BranchId)).ToList();
        var personas = await PersonasAsync(filas0.Select(t => t.PersonId), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Fecha", TipoDeColumna.Fecha), new("Origen"), new("Documento"), new("Clase"), new("Persona"), new("Documento de la persona"), new("Efectivo", TipoDeColumna.Moneda),
            new("Otros medios", TipoDeColumna.Moneda),
        };
        var filas = filas0.Select(t =>
        {
            var p = personas.GetValueOrDefault(t.PersonId);
            var clase = t.DocumentKind switch
            {
                CashTransactionDocumentKind.Receipt => "Recibo",
                CashTransactionDocumentKind.CashDisbursement => "Egreso",
                _ => "Anulación",
            };
            return new FilaExportable([t.OccurredOn, "Recaudo", t.DocumentNumber, $"{clase} ({(t.Direction == CashTransactionDirection.In ? "entra" : "sale")})", p.Nombre,
                p.Documento, t.CashAmount, t.OtherMeansAmount]);
        }).ToList();
        if (puntoDeVenta is not null)
        {
            var pos = await puntoDeVenta.PorPeriodoAsync(desde, hasta, ct);
            var publicos = pos.Select(x => x.PersonPublicId).Distinct().ToList();
            var porPublico = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => publicos.Contains(x.PublicId))
                .Select(x => new { x.PublicId, Nombre = (x.BusinessName ?? (x.FirstName + " " + x.LastName)).Trim(), x.TaxId }).ToDictionaryAsync(x => x.PublicId, ct);
            filas.AddRange(pos.Where(x => a.Oficina is null).Select(x => new FilaExportable([x.OperationDate, "Punto de venta", x.DocumentNumber, "Venta en efectivo",
                porPublico.GetValueOrDefault(x.PersonPublicId)?.Nombre, porPublico.GetValueOrDefault(x.PersonPublicId)?.TaxId, x.CashReceived + x.CashRefunded, 0m])));
        }
        return new TablaExportable("Transacciones en efectivo", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, filas.Sum(f => (decimal)f.Valores[6]!), filas.Sum(f => (decimal)f.Valores[7]!)]),
            ["El registro único suma lo del punto de venta por su consulta, sin copiar filas (FR-204)."]);
    }

    private async Task<TablaExportable> CargaDelPendienteAsync(Ambito a, CancellationToken ct)
    {
        var pendientes = await (from p in db.PendingApplications.AsNoTracking()
                                join r in db.IssuedReceipts.AsNoTracking() on p.ReceiptId equals r.Id
                                where p.Status == PendingApplicationState.Pending
                                orderby p.ValueDate
                                select new { p.PublicId, Recibo = r.Number, p.PersonId, p.TargetModule, p.ProductReference, p.Amount, p.ValueDate, p.BranchId }).ToListAsync(ct);
        pendientes = pendientes.Where(p => a.Alcance.IncluyeOficina(p.BranchId) && (a.Oficina is null || a.Oficina == p.BranchId)).ToList();
        var personas = await PersonasAsync(pendientes.Select(p => p.PersonId), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Módulo"), new("Persona"), new("Documento"), new("Referencia de la carga"), new("Recibo"), new("Fecha valor", TipoDeColumna.Fecha), new("Valor", TipoDeColumna.Moneda),
            new("_pendiente", Clave: "_pendiente"),
        };
        var filas = pendientes.Select(p =>
        {
            var modulo = p.TargetModule == ApplicationTarget.Loan ? "Créditos" : "Cuentas";
            var persona = personas.GetValueOrDefault(p.PersonId);
            return new FilaExportable([modulo, persona.Nombre, persona.Documento, p.ProductReference, p.Recibo, p.ValueDate, p.Amount, p.PublicId], Seccion: modulo);
        }).ToList();
        return new TablaExportable("Carga del recaudo pendiente de aplicar", "Lo que se aplica el día de la activación de cada módulo", columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, pendientes.Sum(p => p.Amount), null]), []);
    }
}
