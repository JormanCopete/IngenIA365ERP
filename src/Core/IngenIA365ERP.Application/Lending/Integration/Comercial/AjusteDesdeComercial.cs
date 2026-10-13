using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>
/// La decisión de un ajuste de Comercial que la operación <c>CommercialAdjustment</c> guarda en su <c>Payload</c> y la reconstrucción lee
/// (<see cref="ContenidoDeAjusteComercial"/>): la clase, el valor con signo, el recálculo sellado y el mensaje y el documento del ajuste. (nuevo)
/// </summary>
public sealed record DecisionDeAjusteComercial
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string AdjustmentClass { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Recalculation { get; init; } = nameof(PrepaymentChoice.ReduceInstallment);

    public Guid AdjustmentMessageId { get; init; }

    public Guid AdjustmentDocumentPublicId { get; init; }

    public string? AdjustmentDocumentNumber { get; init; }

    public string? Reason { get; init; }

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public ContenidoDeAjusteComercial? Contenido() =>
        ContenidoDeAjusteComercial.DeTexto(AdjustmentClass) is { } clase
            ? new ContenidoDeAjusteComercial(clase, Amount) { Recalculo = Enum.TryParse<PrepaymentChoice>(Recalculation, out var r) ? r : PrepaymentChoice.ReduceInstallment }
            : null;

    public static DecisionDeAjusteComercial? Leer(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Trim() == "{}") return null;
        try
        {
            var d = JsonSerializer.Deserialize<DecisionDeAjusteComercial>(payload, Json);
            return d is null || string.IsNullOrEmpty(d.AdjustmentClass) ? null : d;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// El consumo de un <c>AjusteDeVentaACredito</c> (feature 013, US6, T0591; FR-054, FR-143, C17; mensajes.md §12.3, §15.4). Espera a su venta (por la
/// cadena de Comercial y, si llega antes, <c>Retry</c>) y busca la obligación por el recibo de la venta (<c>originalMessageId</c>):
/// <list type="bullet">
/// <item>venta no incorporada (aceptada sin migrar o fuera de Cartera): <c>NotIncorporated</c> y <c>ObligacionRechazada</c> <c>SinObligacion</c>;</item>
/// <item>venta migrada o cancelada antes del corte y ajuste fechado hasta el corte: procesado sin hechos (ya está en el saldo neto);</item>
/// <item><c>CreditNote</c>, <c>Return</c>, <c>DebitNote</c>, <c>Voiding</c>, <c>VoidingByDianRejection</c>: la operación <c>CommercialAdjustment</c> con su
/// decisión; la reconstrucción la aplica desde su fecha valor con el signo que manda (<see cref="ContenidoDeAjusteComercial"/>): baja o sube el capital,
/// reversa el interés de lo devuelto, recalcula el plan según <c>NoteRecalculation</c>, deja el exceso como saldo a favor o, en una anulación, deja el
/// crédito sin efecto desde su origen (<c>Voided</c>); sus filas y las diferencias en los días ya causados por <see cref="RegistroDeCartera"/>; la nota
/// débito se evalúa como una venta a su fecha (<see cref="EvaluacionDeVentaACredito"/>) y, si falla, marca el crédito y entrega
/// <c>ValidationFailed</c>; emite <c>CreditoAjustadoDesdeComercial</c> con <c>related</c> = el desembolso en especie y su acompañante
/// <c>SaldoAFavorReconocido</c>;</item>
/// <item><c>Replacement</c>: un crédito nuevo enlazado al anulado (<c>OriginLoanId</c>), evaluado como una venta, con su <c>CreditoDesembolsado</c>
/// (<c>inKind.replacesLoan</c>) y su <c>ObligacionCreada</c> (<c>replacesLoan</c>) (C17).</item>
/// </list>
/// Corre bajo el cerrojo del deudor y del crédito, en la transacción de quien llama. (nuevo)
/// </summary>
public sealed class AjusteDesdeComercial(
    IApplicationDbContext db,
    RecibosDeCartera recibos,
    ConsumoDeVentaACredito ventas,
    VentasAnterioresAlCorte anteriores,
    EvaluacionDeVentaACredito evaluacion,
    VersionVigenteDeLinea versiones,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    ICerrojoDeCartera cerrojo,
    IngenIA365ERP.Application.Common.Interfaces.Security.IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string PrefijoDeAjuste = "ACO-";
    public const string Reemplazo = "Replacement";

    public async Task<ResultadoDeConsumo> ConsumirAsync(MensajeEntrante m, AjusteDeVentaACreditoV1 a, LendingInboundMessage recibo, LendingActivation? activacion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        recibo.CreditPaymentPublicId = a.OriginalCreditPaymentPublicId;
        var original = await db.LendingInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == a.OriginalMessageId, ct);
        if (original is null || (original.Status == InboundMessageStatus.Rejected && original.NotRetryableAt is null))
            return new ResultadoDeConsumo.Retry("El ajuste espera a su venta: la venta original todavía no tiene obligación en Cartera.");
        if (original.Status == InboundMessageStatus.NotIncorporated || original.Status == InboundMessageStatus.Rejected)
            return await anteriores.AjusteSinObligacionAsync(m, recibo, a.OriginalCreditPaymentPublicId, ct);

        var fecha = m.Envelope.Origin.OperationDate;
        if (original.SaleReconciliationId is not null && VentasAnterioresAlCorte.HastaElCorte(activacion, fecha))
        {
            recibo.Status = InboundMessageStatus.Processed;
            recibo.LoanId = original.LoanId;
            recibo.SaleReconciliationId = original.SaleReconciliationId;
            await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(InboundEvaluationOutcome.Accepted, null,
                "[{\"check\":\"AdjustmentUpToCutoff\",\"passed\":true}]", null, null, false, null), ct);
            return new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational);
        }
        if (original.LoanId is not { } loanId)
            return new ResultadoDeConsumo.Retry("El ajuste espera a su venta: la obligación de la venta todavía no existe.");

        var deudor = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => l.DebtorPersonId).FirstAsync(ct);
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([deudor], ModoDeEspera.Esperar, ct);
        await cerrojo.BloquearCreditosAsync([loanId], ModoDeEspera.Esperar, ct);
        var credito = await db.Loans.FirstAsync(l => l.Id == loanId, ct);
        recibo.PersonId = credito.DebtorPersonId;

        if (string.Equals(a.AdjustmentClass, Reemplazo, StringComparison.Ordinal))
            return await ReemplazarAsync(m, a, recibo, credito, ct);

        if (ContenidoDeAjusteComercial.DeTexto(a.AdjustmentClass) is not { } clase)
            return await ventas.NoConvertibleAsync(m, recibo, new ErrorConDatos(ErroresDePagos.CodigoValidacion,
                $"«{a.AdjustmentClass}» no es una clase de ajuste de una venta a crédito.", new { adjustmentClass = a.AdjustmentClass }), null, ct);
        if (credito.Status == LoanStatus.Voided)
        {
            recibo.Status = InboundMessageStatus.Processed;
            recibo.LoanId = credito.Id;
            await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(InboundEvaluationOutcome.Accepted, null,
                "[{\"check\":\"LoanVoided\",\"passed\":true}]", null, "El crédito ya estaba anulado: el ajuste no tiene efecto.", false, null), ct);
            return new ResultadoDeConsumo.Processed(credito.PublicId, null, credito.Number);
        }
        return await AjustarAsync(m, a, clase, recibo, credito, ct);
    }

    private async Task<ResultadoDeConsumo> AjustarAsync(MensajeEntrante m, AjusteDeVentaACreditoV1 a, ClaseDeAjusteComercial clase, LendingInboundMessage recibo,
        Domain.Entities.Lending.Loans.Loan credito, CancellationToken ct)
    {
        var v = m.Envelope.Origin.OperationDate;
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
        var marca = control.LastAccruedDate;
        var cargado = await cargador.CargarAsync(credito.Id, null, marca > v ? marca : v, ct);
        if (cargado is not { Listo: true })
            return await ventas.NoConvertibleAsync(m, recibo, cargado?.Error ?? ErroresDePagos.ObligacionInexistente(), null, ct);
        var desembolso = await db.LoanOperations.AsNoTracking()
            .Where(o => o.LoanId == credito.Id && o.Kind == LoanOperationKind.Disbursement && o.ReversedByOperationId == null)
            .OrderBy(o => o.ValueDate).ThenBy(o => o.Sequence).FirstAsync(ct);

        // La nota débito se evalúa como una venta a su fecha (FR-054).
        EvaluacionDeVenta? evaluada = null;
        if (clase == ClaseDeAjusteComercial.DebitNote)
        {
            var c = await ComoVentaAsync(m, a, credito, cargado.Vigentes, ct);
            if (c is not null) evaluada = await evaluacion.EvaluarAsync(c, null, false, ct, a.Amount);
        }
        var falla = evaluada?.Falla;

        var contenido = new ContenidoDeAjusteComercial(clase, a.Amount) { Recalculo = cargado.Vigentes.NoteRecalculation };
        var decision = new DecisionDeAjusteComercial
        {
            AdjustmentClass = clase.ToString(), Amount = a.Amount, Recalculation = contenido.Recalculo.ToString(), AdjustmentMessageId = m.MessageId,
            AdjustmentDocumentPublicId = m.Envelope.Origin.PublicId, AdjustmentDocumentNumber = m.Envelope.Origin.Number, Reason = a.Reason,
        };
        var contable = await fechaContable.CalcularAsync(v, ct);
        if (contable.IsFailure) return await ventas.NoConvertibleAsync(m, recibo, contable.Error, null, ct);

        // Primero se prueba el efecto sin registrar nada: un ajuste que la reconstrucción no puede aplicar queda no convertible y reprocesable.
        var entrada = cargado.Entrada!;
        var libro = marca >= v ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
        var prueba = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.CommercialAdjustment, ValueDate = v, RegisteredAt = reloj.UtcNow, Sequence = control.Sequence + 1,
        };
        try
        {
            EfectoEnElLibro.Calcular(entrada, CargadorDeReconstruccion.ConOperacion(entrada, prueba, contenido), CargadorDeReconstruccion.IdEnLaEntrada(prueba),
                v, marca, libro, []);
        }
        catch (Exception e) when (e is ReglaDeCarteraException or ArgumentException)
        {
            var error = e is ReglaDeCarteraException r ? Common.ErroresDeGobierno.De(r.Problema) : new Error(ErroresDePagos.CodigoValidacion, e.Message);
            return await ventas.NoConvertibleAsync(m, recibo, error, null, ct);
        }

        await recibos.AsegurarAsync(recibo, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var payload = decision.Serializar();
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.CommercialAdjustment, PrefijoDeAjuste, ct);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.CommercialAdjustment,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = v,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = a.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = m.Envelope.OriginUser.CentralUserId ?? actor.CentralUserId,
            RelatedOperationId = desembolso.Id,
            InboundMessageId = recibo.Id,
            Reason = a.Reason is null ? null : RecibosDeCartera.Recortar(a.Reason, 300),
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = cargado.Vigentes.RulesVersion,
        });

        // Las filas propias y lo que cambia en los días ya causados (T33).
        var id = CargadorDeReconstruccion.IdEnLaEntrada(operacion);
        var efecto = EfectoEnElLibro.Calcular(entrada, CargadorDeReconstruccion.ConOperacion(entrada, operacion, contenido), id, v, marca, libro, []);

        // El plan regenerado por la nota (FR-054).
        LoanPlanSummaryV1? planNuevo = null;
        if (clase is not (ClaseDeAjusteComercial.Voiding or ClaseDeAjusteComercial.VoidingByDianRejection)
            && efecto.Propias.Any(f => f.Concepto == LoanConcept.Principal && f.Cuota is null) && efecto.Despues.Plan is { } plan)
        {
            var numeroDelPlan = (await db.RepaymentScheduleVersions.AsNoTracking().Where(x => x.LoanId == credito.Id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
            registro.RegistrarPlanRegenerado(credito.Id, numeroDelPlan, cargado.Vigentes, operacion, plan, entrada.Festivos);
            planNuevo = ComprobantesDePago.PlanDelHecho(plan, numeroDelPlan, cargado.Vigentes.Periodicity.ToString());
        }

        // Los hechos, y las filas con el mensaje que las lleva.
        var persona = await db.People.AsNoTracking().Where(p => p.Id == credito.DebtorPersonId).Select(p => new { p.PublicId, p.IsEmployee }).FirstAsync(ct);
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == credito.CreditLineId).Select(l => l.Code).FirstAsync(ct);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return await ventas.NoConvertibleAsync(m, recibo, sucursal.Error, linea, ct);
        var hecho = new HechoDeCredito(credito, cargado.Vigentes, linea, persona.PublicId, persona.IsEmployee, sucursal.Value, contable.Value);
        var anulacion = contenido.EsAnulacion;
        var emitidos = await emision.AjusteComercialAsync(new AjusteComercialAEmitir(hecho, operacion, desembolso, clase.ToString(), m.MessageId,
            EmisionDeCartera.Factura(m), EmisionDeCartera.Relacionado(m) ?? EmisionDeCartera.Factura(m), a.AccountsReceivableRecordedBy, efecto.Propias,
            efecto.DeCausacion, anulacion, planNuevo, falla is not null, NacimientoDeObligacionDeVenta.UsuarioDeOrigen(m)), ct);
        if (emitidos.IsFailure) return await ventas.NoConvertibleAsync(m, recibo, emitidos.Error, linea, ct);
        var u = emitidos.Value;
        registro.RegistrarTransacciones(efecto.Propias.Select(f => RegistroDeCartera.FilaDeHecho(credito.Id, f, operacion, null, contable.Value,
            operacion.RegisteredAt, f.Clase == LoanTransactionKind.CreditBalanceRecognized
                ? u.De(SaldoAFavorReconocidoV1.Type) ?? u.De(CreditoAjustadoDesdeComercialV1.Type)
                : u.De(CreditoAjustadoDesdeComercialV1.Type))));
        registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(credito.Id, efecto.Diferencias, operacion, contable.Value, operacion.RegisteredAt,
            (_, k) => k == LoanTransactionKind.PaymentReallocation ? u.De(CreditoAjustadoDesdeComercialV1.Type) : u.De(AjusteDeCausacionPorFechaValorV1.Type)));

        // El estado: la anulación deja el crédito sin efecto; una nota que lo deja en cero lo cancela; una nota débito sobre uno cancelado lo reabre.
        if (anulacion && RegistroDeCartera.PuedePasar(credito.Status, LoanStatus.Voided))
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.Voided, v, a.Reason ?? $"Anulada la venta en Comercial ({clase}).");
        else if (contenido.Baja && credito.Status == LoanStatus.Active && Math.Max(0m, efecto.Despues.ValorParaCancelar) == 0m)
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.PaidOff, v);
        else if (clase == ClaseDeAjusteComercial.DebitNote && credito.Status == LoanStatus.PaidOff)
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.Active, v);

        // La nota débito evaluada en contra marca el crédito y bloquea al deudor (FR-052).
        if (falla is not null)
        {
            credito.SaleObligationStatus = SaleObligationStatus.ValidationFailed;
            credito.ValidationFailureCode = falla.Code;
            credito.ValidationFailureReason = falla.Reason;
            db.DebtorBlocks.Add(DebtorBlock.Nuevo(credito.DebtorPersonId, DebtorBlockReason.ValidationFailed,
                NacimientoDeObligacionDeVenta.ClaveDelBloqueo(credito.PublicId), v, $"Nota débito {m.Envelope.Origin.Number}: {falla.Reason}", null, credito.Id));
        }

        recibo.Status = falla is null ? InboundMessageStatus.Processed : InboundMessageStatus.ValidationFailed;
        recibo.LoanId = credito.Id;
        await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(falla is null ? InboundEvaluationOutcome.Accepted : InboundEvaluationOutcome.ValidationFailed,
            linea, evaluada?.ChecksJson ?? $"[{{\"check\":\"CommercialAdjustment\",\"passed\":true,\"evidence\":{{\"class\":\"{clase}\"}}}}]",
            falla?.Code, falla?.Reason, false, null), ct);
        await db.SaveChangesAsync(ct);
        return falla is null
            ? new ResultadoDeConsumo.Processed(credito.PublicId, null, credito.Number)
            : new ResultadoDeConsumo.ValidationFailed(credito.PublicId, credito.Number, falla.Code ?? string.Empty, falla.Reason ?? string.Empty);
    }

    /// <summary>El reemplazo (C17): un crédito nuevo enlazado al anulado, evaluado como una venta con las condiciones del valor nuevo.</summary>
    private async Task<ResultadoDeConsumo> ReemplazarAsync(MensajeEntrante m, AjusteDeVentaACreditoV1 a, LendingInboundMessage recibo,
        Domain.Entities.Lending.Loans.Loan anulado, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == anulado.CreditLineId).Select(l => l.Code).FirstAsync(ct);
        var venta = new VentaRecibida(m, anulado.DebtorKind == DebtorKind.Customer ? ConversionDeVentaACredito.Cliente : ConversionDeVentaACredito.Asociado,
            new CreditPaymentV1 { PaymentPublicId = a.OriginalCreditPaymentPublicId, PaymentMeansCode = a.PaymentMeansCode, Amount = a.Amount },
            a.Terms ?? new CreditTermsV1(), linea, null, PendingValidation: true, a.AccountsReceivableRecordedBy, null, null, null);
        return await ventas.CrearAsync(m, venta, recibo, anulado, ct);
    }

    /// <summary>La obligación vista como una venta, para evaluar una nota débito a su fecha (la persona, la línea y la versión vigente).</summary>
    private async Task<VentaConvertida?> ComoVentaAsync(MensajeEntrante m, AjusteDeVentaACreditoV1 a, Domain.Entities.Lending.Loans.Loan credito,
        Domain.Entities.Lending.Loans.LoanTermsVersion condiciones, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == credito.DebtorPersonId, ct);
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == credito.CreditLineId, ct);
        if (persona is null || linea is null) return null;
        var version = await versiones.LeerAsync(linea.Id, m.Envelope.Origin.OperationDate, null, ct);
        if (version is null) return null;
        var venta = new VentaRecibida(m, credito.DebtorKind.ToString(),
            new CreditPaymentV1 { PaymentPublicId = a.OriginalCreditPaymentPublicId, PaymentMeansCode = a.PaymentMeansCode, Amount = a.Amount },
            a.Terms ?? new CreditTermsV1(), linea.Code, null, true, a.AccountsReceivableRecordedBy, null, null, null);
        return new VentaConvertida(venta, persona, credito.DebtorKind, linea, version, condiciones.Periodicity, condiciones.Installments, 0);
    }
}
