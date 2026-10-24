using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Compliance;

/// <summary>
/// El registro propio de transacciones en efectivo de Recaudo (feature 015, US14, T0241; data-model §7.1, §7.2; FR-095, FR-097, FR-204 parte R0): escribe una fila de
/// <c>RCD_CashTransactions</c> en la transacción del recibo con efectivo y, al anularlo, otra con signo contrario; evalúa <see cref="SenalesDeEfectivo"/> con los parámetros
/// vigentes (y su versión) y alerta al oficial (<c>Recaudo.AcumuladoDeEfectivo</c>, <c>Recaudo.Fraccionamiento</c>), una vez por persona, señal y mes, dejando el rastro
/// en <c>RCD_ComplianceAlertsLog</c>. <b>Sólo informa</b>: ninguna señal bloquea. Sin guardar: lo guarda quien llama. En R0 sólo cuenta lo de Recaudo. (nuevo)
/// </summary>
public sealed class RegistroDeEfectivo(IApplicationDbContext db, ILectorDeParametros lector, IAlertas alertas, IDateTimeService reloj,
    IngenIA365ERP.Application.Common.Integration.Inventory.IEfectivoDelPuntoDeVenta? puntoDeVenta = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El recibo confirmado con efectivo: su fila y las señales. Sin efectivo, nada.</summary>
    public async Task AlConfirmarAsync(Receipt recibo, decimal efectivo, int titular, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recibo);
        if (efectivo <= 0m) return;
        var fila = Fila(recibo, recibo.PublicId, recibo.Number, CashTransactionDocumentKind.Receipt, CashTransactionDirection.In, efectivo, titular, recibo.ValueDate);
        db.ReceiptsCashTransactions.Add(fila);
        var senales = await EvaluarAsync(fila, ct);
        fila.AlertsJson = JsonSerializer.Serialize(senales.Select(s => s.Senal.ToString()), Json);
    }

    /// <summary>La anulación de un recibo con efectivo: la fila con signo contrario (el registro nunca se edita).</summary>
    public async Task AlAnularAsync(Receipt recibo, Guid anulacion, string numero, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recibo);
        var original = await db.ReceiptsCashTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.DocumentPublicId == recibo.PublicId && t.DocumentKind == CashTransactionDocumentKind.Receipt, ct);
        if (original is null) return;
        db.ReceiptsCashTransactions.Add(Fila(recibo, anulacion, numero, CashTransactionDocumentKind.Voiding, CashTransactionDirection.In, -original.CashAmount, original.PersonId,
            reloj.HoyLocal));
    }

    /// <summary>
    /// Feature 015, R1 (T0372; FR-099): el egreso de caja entregado es una fila <c>Out</c> del registro y cuenta para las mismas señales (el efectivo entregado suma
    /// para el acumulado y el fraccionamiento del titular y de quien recibe).
    /// </summary>
    public async Task AlEgresarAsync(CashDisbursement egreso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(egreso);
        var fila = FilaDeEgreso(egreso, egreso.PublicId, egreso.Number, CashTransactionDocumentKind.CashDisbursement, egreso.Amount, egreso.OperationDate);
        db.ReceiptsCashTransactions.Add(fila);
        var senales = await EvaluarAsync(fila, ct);
        fila.AlertsJson = JsonSerializer.Serialize(senales.Select(s => s.Senal.ToString()), Json);
    }

    /// <summary>Feature 015, R1 (T0372): la anulación del egreso, la fila <c>Out</c> con signo contrario (el registro nunca se edita).</summary>
    public Task AlAnularEgresoAsync(CashDisbursement egreso, CashDisbursementVoiding anulacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(egreso);
        ArgumentNullException.ThrowIfNull(anulacion);
        db.ReceiptsCashTransactions.Add(FilaDeEgreso(egreso, anulacion.PublicId, anulacion.Number, CashTransactionDocumentKind.Voiding, -egreso.Amount, reloj.HoyLocal));
        return Task.CompletedTask;
    }

    private static CashTransaction FilaDeEgreso(CashDisbursement egreso, Guid documento, string numero, CashTransactionDocumentKind clase, decimal efectivo, DateOnly fecha) => new()
    {
        DocumentPublicId = documento, DocumentNumber = numero.Length <= 30 ? numero : numero[..30], DocumentKind = clase, Direction = CashTransactionDirection.Out,
        PersonId = egreso.BeneficiaryPersonId, PayerPersonId = egreso.ReceivedByPersonId ?? egreso.BeneficiaryPersonId, ReceivedByPersonId = egreso.ReceivedByPersonId,
        CashAmount = efectivo, OtherMeansAmount = 0m, Channel = ReceiptChannel.CashDesk, BranchId = egreso.BranchId, OccurredOn = fecha,
        Month = fecha.Year * 100 + fecha.Month,
    };

    private CashTransaction Fila(Receipt recibo, Guid documento, string numero, CashTransactionDocumentKind clase, CashTransactionDirection sentido, decimal efectivo, int titular,
        DateOnly fecha) => new()
    {
        DocumentPublicId = documento, DocumentNumber = numero.Length <= 30 ? numero : numero[..30], DocumentKind = clase, Direction = sentido, PersonId = titular,
        PayerPersonId = recibo.PayerPersonId, DeliveredByPersonId = recibo.DeliveredByPersonId, CashAmount = efectivo,
        OtherMeansAmount = clase == CashTransactionDocumentKind.Receipt ? Math.Max(0m, recibo.TotalMeans - efectivo - recibo.ChangeGiven) : 0m, Channel = recibo.Channel,
        BranchId = recibo.BranchId, OccurredOn = fecha, Month = fecha.Year * 100 + fecha.Month, DeclarationId = recibo.SourceOfFundsDeclarationId,
    };

    /// <summary>Los parámetros vigentes de las señales con la versión del umbral de múltiples (la que levanta el acumulado).</summary>
    public async Task<ParametrosDeSenales> ParametrosAsync(DateOnly fecha, CancellationToken ct)
    {
        const string m = ParametrosDeRecaudoYTesoreria.ModuloRecaudo;
        var individual = await lector.LeerAsync(m, ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionIndividual, fecha, ct: ct);
        var multiples = await lector.LeerAsync(m, ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionesMultiples, fecha, ct: ct);
        var declaracion = await lector.LeerAsync(m, ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos, fecha, ct: ct);
        var fraccion = await lector.LeerAsync(m, ParametrosDeRecaudoYTesoreria.SarlaftFraccionamiento, fecha, ct: ct);
        var partes = (fraccion.IsSuccess ? fraccion.Value.Texto : string.Empty).Split(';');
        var operaciones = partes.Length == 2 && int.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var o) ? o : 0;
        var dias = partes.Length == 2 && int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : 0;
        var terceros = await lector.LeerAsync(m, ParametrosDeRecaudoYTesoreria.SarlaftPagadorTerceroFrecuenteTitulares, fecha, ct: ct);
        var consignante = await lector.LeerAsync(m, ParametrosDeRecaudoYTesoreria.SarlaftConsignanteRepetidoPartidas, fecha, ct: ct);
        var version = multiples.IsSuccess ? multiples.Value.Vigencia?.PublicId.ToString("N") ?? "defecto" : null;
        return new ParametrosDeSenales(Decimal(individual), Decimal(multiples), Decimal(declaracion), operaciones, dias, Entero(terceros), Entero(consignante),
            version);
    }

    /// <summary>Un entero del lector; si no se puede leer, cero: la señal no se evalúa (nunca un literal de negocio aquí).</summary>
    private static int Entero(IngenIA365ERP.Application.Common.Models.Result<ValorDeParametro> r) =>
        r.IsSuccess && r.Value.Valor is int v ? v : 0;

    private static decimal Decimal(IngenIA365ERP.Application.Common.Models.Result<ValorDeParametro> r) =>
        r.IsSuccess && r.Value.Valor is decimal v ? v : 0m;

    /// <summary>
    /// Feature 015, R1 (T0390; FR-204): el efectivo del punto de venta de <paramref name="personas"/> entre dos fechas como transacciones de efectivo (recibido y devuelto
    /// cuentan igual, como los egresos de caja, FR-099). Sin puerto, nada.
    /// </summary>
    public async Task<IReadOnlyList<TransaccionDeEfectivo>> DelPuntoDeVentaAsync(IReadOnlyCollection<int> personas, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (puntoDeVenta is null || personas.Count == 0) return [];
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        return (await puntoDeVenta.PorPeriodoAsync(desde, hasta, ct))
            .Where(e => publicos.ContainsKey(e.PersonPublicId) && e.CashReceived + e.CashRefunded > 0m)
            .Select(e => new TransaccionDeEfectivo(publicos[e.PersonPublicId], publicos[e.PersonPublicId], e.OperationDate, e.CashReceived + e.CashRefunded))
            .ToList();
    }

    private async Task<IReadOnlyList<SenalDetectada>> EvaluarAsync(CashTransaction nueva, CancellationToken ct)
    {
        var p = await ParametrosAsync(nueva.OccurredOn, ct);
        var desde = new DateOnly(nueva.OccurredOn.Year, nueva.OccurredOn.Month, 1).AddDays(-Math.Max(0, p.FraccionamientoDias));
        var personas = new[] { nueva.PersonId, nueva.PayerPersonId };
        var previas = await db.ReceiptsCashTransactions.AsNoTracking()
            .Where(t => (personas.Contains(t.PersonId) || personas.Contains(t.PayerPersonId)) && t.OccurredOn >= desde && t.OccurredOn <= nueva.OccurredOn)
            .Select(t => new TransaccionDeEfectivo(t.PersonId, t.PayerPersonId, t.OccurredOn, t.CashAmount)).ToListAsync(ct);
        previas.Add(new TransaccionDeEfectivo(nueva.PersonId, nueva.PayerPersonId, nueva.OccurredOn, nueva.CashAmount));
        // Feature 015, R1 (T0390; FR-204; E-03): el registro único suma el efectivo del punto de venta por su puerto, sin copiar sus filas.
        previas.AddRange(await DelPuntoDeVentaAsync(personas, desde, nueva.OccurredOn, ct));
        var senales = SenalesDeEfectivo.Evaluar(nueva.PersonId, previas, nueva.OccurredOn, p).ToList();
        if (nueva.PayerPersonId != nueva.PersonId) senales.AddRange(SenalesDeEfectivo.Evaluar(nueva.PayerPersonId, previas, nueva.OccurredOn, p));
        var mes = nueva.Month.ToString(CultureInfo.InvariantCulture);
        foreach (var s in senales)
        {
            var tipo = s.Senal switch
            {
                SenalDeEfectivo.Accumulated => TiposDeAlerta.RecaudoAcumuladoDeEfectivo,
                SenalDeEfectivo.Fragmentation => TiposDeAlerta.RecaudoFraccionamiento,
                _ => null,
            };
            Guid? alerta = null;
            if (tipo is not null)
            {
                var levantada = await alertas.LevantarAsync(new AlertaALevantar(tipo, s.Senal == SenalDeEfectivo.Accumulated ? "Acumulado de efectivo del mes" : "Posible fraccionamiento",
                    string.Create(CultureInfo.InvariantCulture,
                        $"{s.Cantidad} operación(es) en efectivo por {s.Total:N0} entre el {s.Desde:dd/MM/yyyy} y el {s.Hasta:dd/MM/yyyy} (parámetro {p.Version})."),
                    "Person", await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == s.PersonId).Select(x => x.PublicId).FirstAsync(ct),
                    DedupKey: $"{tipo}:{s.PersonId}:{mes}", ModuloDeOrigen: "Receipts", RecipientPermissions: ["Receipts.Compliance.View"]), ct);
                alerta = levantada.IsSuccess ? levantada.Value.AlertPublicId : null;
            }
            db.ReceiptsComplianceAlertLogs.Add(new ComplianceAlertLog
            {
                PersonId = s.PersonId, Signal = s.Senal, WindowFrom = s.Desde, WindowTo = s.Hasta, Count = s.Cantidad, Total = s.Total, ParameterVersion = p.Version,
                AlertPublicId = alerta, DocumentPublicId = nueva.DocumentPublicId, EvaluatedAt = reloj.UtcNow,
            });
        }
        return senales;
    }
}
