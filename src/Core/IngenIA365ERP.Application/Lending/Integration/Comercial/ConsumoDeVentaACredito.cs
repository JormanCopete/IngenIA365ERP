using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>
/// Una venta a crédito en vivo (o el reemplazo de una venta anulada, C17), paso a paso (feature 013, US6, T0587; mensajes.md §15.3 pasos 4 a 9; T17):
/// <list type="number">
/// <item>la conversión (<see cref="ConversionDeVentaACredito"/>): lo no convertible deja el recibo <c>Rejected</c> y reprocesable, con su evaluación y
/// <b>sin</b> <c>ObligacionRechazada</c> (C95);</item>
/// <item>el plan de la venta (<see cref="NacimientoDeObligacionDeVenta.SimularAsync"/>) y la validación previa contable, fuera del cerrojo (sólo advierte y
/// sella <c>PrevalidationOutcome</c>, T56, D9);</item>
/// <item>el cerrojo: <c>LND_Setup</c> compartido → <c>LND_DebtorControls</c> del deudor (T34): en orden de llegada, lo ya creado cuenta;</item>
/// <item>con <c>pendingValidation</c>, la evaluación de FR-051 a la fecha de la venta (<see cref="EvaluacionDeVentaACredito"/>); sin ella, no se reevalúa;</item>
/// <item>la obligación nace siempre (<see cref="NacimientoDeObligacionDeVenta"/>) y, en el mismo guardado, <c>ObligacionCreada</c>
/// (<c>Validated</c> o <c>ValidationFailed</c> con su motivo);</item>
/// <item>el recibo queda <c>Processed</c> o <c>ValidationFailed</c> con su evaluación, y la respuesta a la plataforma es <c>Processed(crédito, número)</c> o
/// <c>ValidationFailed(crédito, número, código, motivo)</c>.</item>
/// </list>
/// Guarda en la transacción de quien llama. (nuevo)
/// </summary>
public sealed class ConsumoDeVentaACredito(
    IApplicationDbContext db,
    RecibosDeCartera recibos,
    ConversionDeVentaACredito conversion,
    EvaluacionDeVentaACredito evaluacion,
    NacimientoDeObligacionDeVenta nacimiento,
    ValidacionPreviaContableDeCartera validacionPrevia,
    EmisionDeCartera emision,
    ICerrojoDeCartera cerrojo)
{
    public async Task<ResultadoDeConsumo> CrearAsync(MensajeEntrante m, VentaRecibida venta, LendingInboundMessage recibo, Loan? reemplaza, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(venta);
        recibo.CreditPaymentPublicId = venta.Pago.PaymentPublicId;
        recibo.ReservationPublicId = venta.ReservationPublicId;
        recibo.ReservedLoanNumber = venta.ReservedLoanNumber is null ? null : RecibosDeCartera.Recortar(venta.ReservedLoanNumber, 30);

        var convertida = await conversion.ConvertirAsync(venta, ct);
        if (convertida.IsFailure) return await NoConvertibleAsync(m, recibo, convertida.Error, null, ct);
        var c = convertida.Value;
        recibo.PersonId = c.Persona.Id;

        var simulada = await nacimiento.SimularAsync(c, venta.Pago.Amount, ct);
        if (simulada.IsFailure) return await NoConvertibleAsync(m, recibo, simulada.Error, c.Linea.Code, ct);
        var sim = simulada.Value.Simulacion;

        // La validación previa contable, fuera del cerrojo: sólo advierte (T56, D9).
        var sucursal = await SucursalAsync(m, ct);
        var previa = (await validacionPrevia.DecidirAsync(
            [RequisitosDelDesembolso.HechoParaValidar(c.Linea, sim, c.Persona, sucursal, venta.Fecha, venta.Pago.Amount, 1, DisbursementForm.InKind)],
            OrigenDeValidacionPrevia.ConsumidorDeComercial, $"la venta {venta.NumeroDeFactura}", ct)).Resultado;

        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([c.Persona.Id], ModoDeEspera.Esperar, ct);

        var evaluada = venta.PendingValidation
            ? await evaluacion.EvaluarAsync(c, sim.Usura, simulada.Value.FueraDeRango, ct)
            : EvaluacionDeVenta.SinReevaluar;

        await recibos.AsegurarAsync(recibo, ct);
        var nacida = await nacimiento.NacerAsync(new PedidoDeObligacionDeVenta(c, sim, evaluada, recibo, previa.Outcome, reemplaza), ct);
        if (nacida.IsFailure) return await NoConvertibleAsync(m, recibo, nacida.Error, c.Linea.Code, ct);
        var o = nacida.Value;
        if (o.ReservaNoEncontrada)
            evaluada = evaluada.Con(new VerificacionDeVenta("Reservation", true, ErroresDeVentasACredito.CodigoReservaInexistente,
                $"La reserva de la venta no existe o no coincide: el crédito nace con el número {o.Credito.Number}.",
                new { reservation = venta.ReservationPublicId, reservedNumber = venta.ReservedLoanNumber, assigned = o.Credito.Number }));

        var falla = evaluada.Falla;
        var respuesta = await emision.ObligacionCreadaAsync(new ObligacionCreadaAEmitir(recibo, m, venta.Pago.PaymentPublicId, o.Credito, c.Linea.Code,
            o.Credito.ApprovedAmount, venta.Fecha,
            falla is null ? nameof(SaleObligationStatus.Validated) : nameof(SaleObligationStatus.ValidationFailed),
            falla is null ? null : new SaleValidationV1 { Code = falla.Code ?? string.Empty, Reason = falla.Reason ?? string.Empty },
            o.Plan, o.FuenteDelNumero, o.Reserva?.PublicId,
            reemplaza is null ? null : new LoanRefV1 { LoanPublicId = reemplaza.PublicId, LoanNumber = reemplaza.Number }, o.SucursalPublicId), ct);

        recibo.Status = falla is null ? InboundMessageStatus.Processed : InboundMessageStatus.ValidationFailed;
        recibo.LoanId = o.Credito.Id;
        recibo.ResponseMessagePublicId = respuesta.PublicId;
        await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(falla is null ? InboundEvaluationOutcome.Accepted : InboundEvaluationOutcome.ValidationFailed,
            c.Linea.Code, evaluada.ChecksJson, falla?.Code, falla?.Reason, false, previa.Outcome.ToString()), ct);
        await db.SaveChangesAsync(ct);

        return falla is null
            ? new ResultadoDeConsumo.Processed(o.Credito.PublicId, null, o.Credito.Number)
            : new ResultadoDeConsumo.ValidationFailed(o.Credito.PublicId, o.Credito.Number, falla.Code ?? string.Empty, falla.Reason ?? string.Empty);
    }

    /// <summary>
    /// Lo no convertible (FR-053, C95): el recibo queda <c>Rejected</c> y reprocesable con su evaluación, sin respuesta a Comercial —la venta se informa
    /// <c>Pending</c> con su motivo, C40— hasta que una persona lo marque no reprocesable (C102).
    /// </summary>
    public async Task<ResultadoDeConsumo> NoConvertibleAsync(MensajeEntrante m, LendingInboundMessage recibo, Error error, string? linea, CancellationToken ct)
    {
        recibo.Status = InboundMessageStatus.Rejected;
        var datos = error is ErrorConDatos d ? System.Text.Json.JsonSerializer.Serialize(d.Data, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) : null;
        await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(InboundEvaluationOutcome.NotConvertible, linea,
            $"[{{\"check\":\"Conversion\",\"passed\":false,\"code\":\"{error.Code}\"}}]", error.Code, error.Message, true, null), ct);
        return new ResultadoDeConsumo.Rejected(error.Code, error.Message, datos);
    }

    /// <summary>La sucursal de la venta si existe en la cooperativa; si no, la principal.</summary>
    private async Task<Guid> SucursalAsync(MensajeEntrante m, CancellationToken ct) =>
        await db.Branches.AsNoTracking().AnyAsync(b => b.PublicId == m.Envelope.BranchPublicId && !b.IsDeleted, ct)
            ? m.Envelope.BranchPublicId
            : await emision.SucursalPrincipalAsync(ct);
}
