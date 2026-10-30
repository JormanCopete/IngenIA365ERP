using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reports;

// Feature 015, T1 (fase 35, T0423; FR-153 parte T1; api.md §17): las vistas de T1 del centro de informes de Tesorería —comprobantes de pago, traslados entre cuentas y el
// cierre del mes de los cheques—, con el alcance por cuenta propia de las demás. Sólo leen.
public sealed partial class TreasuryReportQueryHandler
{
    private async Task<TablaExportable> ComprobantesDePagoAsync(Func<int, bool> ve, Dictionary<int, OwnBankAccount> cuentas, DateOnly desde, DateOnly hasta,
        CancellationToken ct)
    {
        var filas = await (from v in db.PaymentVouchers.AsNoTracking()
                           join o in db.PaymentOrders.AsNoTracking() on v.PaymentOrderId equals o.Id
                           where v.PaidOn >= desde && v.PaidOn <= hasta
                           orderby v.PaidOn, v.Number
                           select new
                           {
                               v.PublicId, v.Number, v.PaidOn, v.BankAccountId, Orden = o.Number, o.Origin, o.BeneficiaryName, v.Form, v.GrossAmount, v.WithholdingAmount,
                               v.NetAmount, v.TreasuryReference, v.CheckNumber, v.SentAt,
                           }).ToListAsync(ct);
        var visibles = filas.Where(f => f.BankAccountId is not int c || ve(c)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Comprobante"), new("Fecha", TipoDeColumna.Fecha), new("Cuenta de origen"), new("Orden"), new("Origen"), new("Beneficiario"), new("Forma"),
            new("Bruto", TipoDeColumna.Moneda), new("Retención", TipoDeColumna.Moneda), new("Neto", TipoDeColumna.Moneda), new("Referencia"), new("Enviado"),
            new("_comprobante", Clave: "_comprobante"),
        };
        var tabla = visibles.Select(f => new FilaExportable(
        [
            f.Number, f.PaidOn, f.BankAccountId is int c ? Cuenta(cuentas, c) : string.Empty, f.Orden, f.Origin.ToString(), f.BeneficiaryName, f.Form.ToString(), f.GrossAmount,
            f.WithholdingAmount, f.NetAmount, f.CheckNumber ?? f.TreasuryReference, f.SentAt is null ? "No" : "Sí", f.PublicId,
        ])).ToList();
        return new TablaExportable("Comprobantes de pago", Periodo(desde, hasta), columnas, tabla,
            new FilaExportable(["Total", null, null, null, null, null, null, visibles.Sum(f => f.GrossAmount), visibles.Sum(f => f.WithholdingAmount), visibles.Sum(f => f.NetAmount),
                null, null, null]),
            ["Cada pago girado deja su comprobante numerado con código de verificación; se envía al beneficiario por correo sólo con su autorización (FR-117, FR-020)."]);
    }

    private async Task<TablaExportable> TrasladosAsync(Func<int, bool> ve, Dictionary<int, OwnBankAccount> cuentas, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var filas = (await db.TreasuryTransfers.AsNoTracking().Where(t => t.RequestedOn >= desde && t.RequestedOn <= hasta).OrderBy(t => t.RequestedOn).ThenBy(t => t.Number)
                .ToListAsync(ct))
            .Where(t => ve(t.FromBankAccountId) || ve(t.ToBankAccountId)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Traslado"), new("Pedido", TipoDeColumna.Fecha), new("De"), new("A"), new("Valor", TipoDeColumna.Moneda), new("Motivo"), new("Estado"),
            new("Enviado", TipoDeColumna.Fecha), new("Confirmado", TipoDeColumna.Fecha), new("Días en tránsito", TipoDeColumna.Entero), new("_traslado", Clave: "_traslado"),
        };
        var tabla = filas.Select(t =>
        {
            var transito = t.Status == TransferState.Sent && t.SentOn is { } s ? hoy.DayNumber - s.DayNumber : 0;
            return new FilaExportable(
            [
                t.Number, t.RequestedOn, Cuenta(cuentas, t.FromBankAccountId), Cuenta(cuentas, t.ToBankAccountId), t.Amount, t.Reason, t.Status.ToString(), t.SentOn,
                t.ConfirmedOn, transito, t.PublicId,
            ], Resaltada: t.Status == TransferState.Sent);
        }).ToList();
        return new TablaExportable("Traslados entre cuentas propias", Periodo(desde, hasta), columnas, tabla,
            new FilaExportable(["Total", null, null, null, filas.Where(t => t.Status is TransferState.Sent or TransferState.Confirmed).Sum(t => t.Amount), null, null, null, null,
                null, null]),
            ["Un traslado enviado queda en tránsito hasta que el extracto de la cuenta de destino lo confirma o se confirma con la constancia del banco (FR-104)."]);
    }

    private async Task<TablaExportable> CierresDeChequesAsync(Func<int, bool> ve, Dictionary<int, OwnBankAccount> cuentas, DateOnly desde, DateOnly hasta,
        CancellationToken ct)
    {
        var filas = (await db.MonthEndCheckRuns.AsNoTracking().Where(r => r.CutoffDate >= desde && r.CutoffDate <= hasta).OrderBy(r => r.CutoffDate).ThenBy(r => r.Number)
                .ToListAsync(ct))
            .Where(r => ve(r.BankAccountId)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Cierre"), new("Mes"), new("Cuenta"), new("Corte", TipoDeColumna.Fecha), new("Plazo a cuenta por pagar (días)", TipoDeColumna.Entero),
            new("Reclasificados", TipoDeColumna.Entero), new("Valor reclasificado", TipoDeColumna.Moneda), new("A cuenta por pagar", TipoDeColumna.Entero),
            new("Valor a cuenta por pagar", TipoDeColumna.Moneda), new("_cierre", Clave: "_cierre"),
        };
        var tabla = filas.Select(r => new FilaExportable(
        [
            r.Number, $"{r.PeriodYear}-{r.PeriodMonth:00}", Cuenta(cuentas, r.BankAccountId), r.CutoffDate, r.DaysToPayable, r.ReclassifiedCount, r.ReclassifiedAmount,
            r.MovedToPayableCount, r.MovedToPayableAmount, r.PublicId,
        ])).ToList();
        return new TablaExportable("Cierre del mes de los cheques girados", Periodo(desde, hasta), columnas, tabla,
            new FilaExportable(["Total", null, null, null, null, filas.Sum(r => r.ReclassifiedCount), filas.Sum(r => r.ReclassifiedAmount), filas.Sum(r => r.MovedToPayableCount),
                filas.Sum(r => r.MovedToPayableAmount), null]),
            ["Los emitidos y no entregados se reclasifican al efectivo de la cuenta y vuelven el primer día del mes siguiente; los no cobrados más allá del plazo pasan a cuenta por pagar al beneficiario, nunca a ingreso (FR-220)."]);
    }
}
