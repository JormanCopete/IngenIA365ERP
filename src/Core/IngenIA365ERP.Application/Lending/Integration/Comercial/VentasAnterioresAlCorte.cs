using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>
/// Las ventas y los ajustes fechados <b>hasta el corte</b> de la carga activada (feature 013, US6, T0592; FR-143, C63, C99; mensajes.md §15.4;
/// data-model §21.1, §21.5): <b>nunca se procesan en vivo</b>; el consumidor responde según la fila vigente de <c>LND_MigrationSaleReconciliations</c>
/// (la versión del cuadre de la activación):
/// <list type="bullet">
/// <item><c>Migrated</c> / <c>CancelledBeforeCutoff</c>: la obligación ya nació en la activación (<c>ResultingLoanId</c>): recibo <c>Processed</c> y
/// <c>ObligacionCreada</c> con ese estado y <c>numberSource = Migration</c> (en cero y sin plan la cancelada);</item>
/// <item><c>NotMigratedWithBalance</c> (aceptada con acta) / <c>OutsideLendingD29</c>: recibo <c>NotIncorporated</c> y <c>ObligacionRechazada</c>
/// <c>AceptadaSinMigrar</c> / <c>AceptadaFueraDeCartera</c>, no reprocesable;</item>
/// <item><b>sin fila</b> (emitida después de activar): <c>NotIncorporated</c>, <c>ObligacionRechazada</c> <c>Lending.Sale.NotIncorporated</c> no
/// reprocesable y la alerta <c>Cartera.VentaNoIncorporada</c> (C63, C99; dueño: pregunta I16, propuesta: fuera de Cartera).</item>
/// </list>
/// La entrega queda <c>Processed</c> con la referencia «no incorporada» (<c>MotivoSinComprobante.NotIncorporated</c>, C21). Los ajustes ≤ corte de una
/// venta migrada se dan por procesados sin hechos (están en el saldo neto). Guarda en la transacción de quien llama. (nuevo)
/// </summary>
public sealed class VentasAnterioresAlCorte(IApplicationDbContext db, RecibosDeCartera recibos, EmisionDeCartera emision, IAlertas alertas)
{
    /// <summary>¿Está la fecha de la venta o del ajuste hasta el corte de la carga activada?</summary>
    public static bool HastaElCorte(LendingActivation? activacion, DateOnly fecha) => activacion is not null && fecha <= activacion.CutoffDate;

    /// <summary>La venta fechada hasta el corte: responde según su conciliación.</summary>
    public async Task<ResultadoDeConsumo> VentaAsync(MensajeEntrante m, VentaRecibida venta, LendingInboundMessage recibo, LendingActivation activacion,
        CancellationToken ct)
    {
        var fila = await db.MigrationSaleReconciliations.AsNoTracking()
            .Where(r => r.MigrationLoadId == activacion.MigrationLoadId && r.CheckVersion == activacion.CheckVersion && !r.IsDeleted
                        && (r.CreditPaymentPublicId == venta.Pago.PaymentPublicId || r.MessagePublicId == m.MessageId))
            .FirstOrDefaultAsync(ct);
        recibo.CreditPaymentPublicId = venta.Pago.PaymentPublicId;
        recibo.PersonId = fila?.PersonId ?? await PersonaAsync(m, ct);
        var sucursal = await SucursalAsync(m, ct);

        if (fila is null)
        {
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.VentaNoIncorporada, "Venta a crédito no incorporada",
                $"La venta {venta.NumeroDeFactura} del {venta.Fecha:yyyy-MM-dd} es anterior o igual al corte de la migración ({activacion.CutoffDate:yyyy-MM-dd}) y "
                + "llegó después de activar Cartera sin estar en la conciliación: no se incorporó. Regístrela como venta nueva o gestiónela por fuera de Cartera.",
                EntityType: "SaleMessage", EntityPublicId: m.MessageId, DedupKey: $"{TiposDeAlerta.VentaNoIncorporada}:{m.MessageId:D}",
                ModuloDeOrigen: "Lending"), ct);
            return await NoIncorporadaAsync(m, recibo, venta.Pago.PaymentPublicId, null, EmisionDeCartera.MotivoAceptadaFueraDeCartera,
                ErroresDeVentasACredito.MotivoSinFila, sucursal, ct);
        }
        recibo.SaleReconciliationId = fila.Id;

        switch (fila.Outcome)
        {
            case SaleReconciliationOutcome.Migrated or SaleReconciliationOutcome.CancelledBeforeCutoff:
                var credito = fila.ResultingLoanId is { } id ? await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct) : null;
                if (credito is null)
                    return new ResultadoDeConsumo.Rejected(ErroresDeVentasACredito.CodigoNoIncorporada,
                        $"La venta {venta.NumeroDeFactura} está conciliada como {fila.Outcome} pero su obligación todavía no nació en la activación: reprocese el mensaje después de activar.");
                var cancelada = fila.Outcome == SaleReconciliationOutcome.CancelledBeforeCutoff;
                var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == credito.CreditLineId).Select(l => l.Code).FirstAsync(ct);
                recibo.Status = InboundMessageStatus.Processed;
                recibo.LoanId = credito.Id;
                var evaluacion = await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(InboundEvaluationOutcome.Accepted, linea,
                    $"[{{\"check\":\"MigrationReconciliation\",\"passed\":true,\"evidence\":{{\"outcome\":\"{fila.Outcome}\"}}}}]", null, null, false, null), ct);
                var respuesta = await emision.ObligacionCreadaAsync(new ObligacionCreadaAEmitir(recibo, m, venta.Pago.PaymentPublicId, credito, linea,
                    cancelada ? 0m : credito.ApprovedAmount, venta.Fecha, cancelada ? nameof(SaleObligationStatus.CancelledBeforeCutoff) : nameof(SaleObligationStatus.Migrated),
                    null, cancelada ? null : await PlanResumidoAsync(credito, ct), EmisionDeCartera.FuenteMigracion, null, null, sucursal), ct);
                recibo.ResponseMessagePublicId = respuesta.PublicId;
                _ = evaluacion;
                await db.SaveChangesAsync(ct);
                return new ResultadoDeConsumo.Processed(credito.PublicId, null, credito.Number);
            case SaleReconciliationOutcome.NotMigratedWithBalance:
                return await NoIncorporadaAsync(m, recibo, venta.Pago.PaymentPublicId, fila.Id, EmisionDeCartera.MotivoAceptadaSinMigrar,
                    ErroresDeVentasACredito.MotivoAceptadaSinMigrar, sucursal, ct);
            default:
                return await NoIncorporadaAsync(m, recibo, venta.Pago.PaymentPublicId, fila.Id, EmisionDeCartera.MotivoAceptadaFueraDeCartera,
                    ErroresDeVentasACredito.MotivoAceptadaFueraDeCartera, sucursal, ct);
        }
    }

    /// <summary>
    /// Un ajuste de una venta que no se incorporó (aceptada sin migrar o fuera de Cartera): <c>NotIncorporated</c> y <c>ObligacionRechazada</c>
    /// <c>SinObligacion</c>, no reprocesable (T17, T18).
    /// </summary>
    public async Task<ResultadoDeConsumo> AjusteSinObligacionAsync(MensajeEntrante m, LendingInboundMessage recibo, Guid pagoOriginal, CancellationToken ct)
    {
        recibo.CreditPaymentPublicId = pagoOriginal;
        recibo.PersonId ??= await PersonaAsync(m, ct);
        return await NoIncorporadaAsync(m, recibo, pagoOriginal, null, EmisionDeCartera.MotivoSinObligacion, ErroresDeVentasACredito.MotivoSinObligacion,
            await SucursalAsync(m, ct), ct);
    }

    private async Task<ResultadoDeConsumo> NoIncorporadaAsync(MensajeEntrante m, LendingInboundMessage recibo, Guid pago, int? fila, string motivo,
        string razon, Guid sucursal, CancellationToken ct)
    {
        recibo.Status = InboundMessageStatus.NotIncorporated;
        if (fila is not null) recibo.SaleReconciliationId = fila;
        var evaluacionId = Guid.NewGuid();
        var respuesta = await emision.ObligacionRechazadaAsync(new ObligacionRechazadaAEmitir(recibo, m, pago, evaluacionId, motivo,
            ErroresDeVentasACredito.CodigoNoIncorporada, razon, sucursal), ct);
        await recibos.AnotarAsync(recibo, m, new EvaluacionAAnotar(InboundEvaluationOutcome.NotIncorporated, null,
            $"[{{\"check\":\"MigrationReconciliation\",\"passed\":false,\"evidence\":{{\"motive\":\"{motivo}\"}}}}]",
            ErroresDeVentasACredito.CodigoNoIncorporada, razon, false, null, respuesta.PublicId, evaluacionId), ct);
        recibo.ResponseMessagePublicId = respuesta.PublicId;
        await db.SaveChangesAsync(ct);
        return new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.NotIncorporated);
    }

    private async Task<int?> PersonaAsync(MensajeEntrante m, CancellationToken ct) =>
        m.Envelope.PersonPublicId is { } p
            ? await db.People.AsNoTracking().Where(x => x.PublicId == p && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;

    /// <summary>La sucursal del sobre de la respuesta: la de la venta si existe en la cooperativa; si no, la principal.</summary>
    public async Task<Guid> SucursalAsync(MensajeEntrante m, CancellationToken ct) =>
        await db.Branches.AsNoTracking().AnyAsync(b => b.PublicId == m.Envelope.BranchPublicId && !b.IsDeleted, ct)
            ? m.Envelope.BranchPublicId
            : await emision.SucursalPrincipalAsync(ct);

    /// <summary>El resumen del último plan del crédito (para la respuesta de una venta migrada).</summary>
    public async Task<LoanPlanSummaryV1?> PlanResumidoAsync(Loan credito, CancellationToken ct)
    {
        var plan = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == credito.Id).OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
        var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).OrderByDescending(t => t.VersionNumber).FirstOrDefaultAsync(ct);
        if (plan is null || condiciones is null) return null;
        var cuotas = await db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == plan.Id).OrderBy(i => i.InstallmentNumber).ToListAsync(ct);
        if (cuotas.Count == 0) return null;
        return new LoanPlanSummaryV1
        {
            ScheduleVersion = plan.VersionNumber,
            AmortizationSystem = condiciones.AmortizationSystem.ToString(),
            Periodicity = condiciones.Periodicity.ToString(),
            InstallmentCount = cuotas.Count,
            InstallmentAmount = cuotas[0].Total,
            FirstDueDate = cuotas[0].DueDate,
            FinalDueDate = cuotas[^1].DueDate,
            AnnualEffectiveRate = condiciones.AnnualRate ?? 0m,
            DayCountConvention = condiciones.DayCount.ToString(),
            RulesVersion = plan.RulesVersion,
        };
    }
}
