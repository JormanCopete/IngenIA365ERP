using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>Una resolución que el contrato registra: quién recauda, la solicitud y la validación previa. (nuevo)</summary>
public sealed record ResolucionEnCurso(Recaudador Recaudador, SolicitudDeResolucion Solicitud, PrevalidationOutcome ValidacionPrevia);

/// <summary>Una resolución registrada (o la original con la misma clave): su operación, el comprobante y los mensajes. (nuevo)</summary>
public sealed record ResolucionRegistrada(LoanOperation Operacion, ComprobanteDeResolucionDto Comprobante, UnidadEmitida Mensajes, bool YaResuelta);

/// <summary>
/// La <b>resolución de un saldo a favor o de un anticipo</b> (feature 013, US5, T0544; FR-076, FR-083, D7, D24; interfaces §3.9; data-model §7.10).
/// Bajo el cerrojo de las dos obligaciones (en orden de Id, lo toma el contrato) y en la transacción de quien llama:
/// <list type="bullet">
/// <item>la operación <c>CreditBalanceResolution</c> fechada hoy, con su decisión (<see cref="DecisionDeResolucion"/>), baja el saldo a favor o el
/// anticipo (<c>CreditBalanceSettled</c>); más de lo disponible: <c>Lending.CreditBalance.Insufficient</c>;</item>
/// <item><c>RefundThroughCollector</c>: sólo la baja; el recaudador entrega el dinero por su proceso;</item>
/// <item><c>ApplyToOtherObligation</c>: una obligación <b>del mismo deudor</b> (<c>Lending.CreditBalance.TargetNotOwned</c>) recibe un pago por
/// <see cref="AplicacionDePagos"/> con la clase <c>InternalOffset</c>, pagador el deudor, <c>RelatedOperationId</c> = la resolución y la clave
/// <c>{clave}:destino</c>;</item>
/// <item><c>ApplyAsExtraordinaryPrincipal</c>: sólo un saldo a favor que nació de un abono sin preferencia (D24; si no,
/// <c>Lending.Payment.ModalityNotAllowed</c>), con la elección del deudor y la versión nueva del plan.</item>
/// </list>
/// Emite <c>SaldoAFavorResuelto</c>. Un saldo a favor en una obligación <c>PaidOff</c> se resuelve aquí. (nuevo)
/// </summary>
public sealed class ResolucionDeSaldoAFavor(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    PuestaAlDiaDeCredito puestaAlDia,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    PartesDeHechosDeCartera partes,
    FechaContable fechaContable,
    AplicacionDePagos aplicacion,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string PrefijoDeResolucion = "RSF-";

    public const string MetodoResolver = "Lending.ObligationContract.ResolverSaldoAFavor";

    /// <summary>La clave de la aplicación destino (§3.10).</summary>
    public static string ClaveDeDestino(string clave) => clave + ClavesDeRecaudador.SufijoDestino;

    public async Task<Result<ResolucionRegistrada>> ResolverAsync(ResolucionEnCurso r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var s = r.Solicitud;
        var huella = HuellaDeOperacion.Calcular(MetodoResolver, new { collector = r.Recaudador.Code, request = s with { SimulationId = null } });
        var credito = await db.Loans.FirstOrDefaultAsync(l => l.PublicId == s.ObligationPublicId && !l.IsDeleted, ct);
        if (credito is null) return Falla(ErroresDePagos.ObligacionInexistente());

        var existente = await db.LoanOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.CollectorCode == r.Recaudador.Code && o.IdempotencyKey == s.IdempotencyKey, ct);
        if (existente is not null)
            return existente.RequestSha256 == huella && existente.LoanId == credito.Id
                ? Result.Success(new ResolucionRegistrada(existente, await ComprobanteAsync(existente, credito, ct), UnidadEmitida.Vacia, true))
                : Falla(ErroresDePagos.ClaveReusada(s.IdempotencyKey));

        var concepto = ComprobantesDePago.Concepto(s.Concept);
        var hoy = reloj.HoyLocal;
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
        var deOtro = puestaAlDia.EstaPendiente(credito.Id);
        var preparada = false;
        if (!deOtro && control.LastAccruedDate < hoy)
        {
            await puestaAlDia.PrepararAsync([credito.Id], hoy, ct);
            preparada = puestaAlDia.EstaPendiente(credito.Id);
        }
        var marca = control.LastAccruedDate;
        var cargado = await cargador.CargarAsync(credito.Id, null, marca > hoy ? marca : hoy, ct);
        if (cargado is null) return Falla(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        if (!cargado.Listo) return Falla(cargado.Error!);
        var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, hoy);
        var disponible = estado.Saldo(concepto);
        if (s.Amount > disponible) return Falla(ErroresDePagos.SaldoInsuficiente(s.Amount, disponible));

        // Las reglas de cada forma, antes de escribir nada.
        Loan? destino = null;
        if (s.Form == FormaDeResolucion.ApplyToOtherObligation)
        {
            destino = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == s.TargetObligationPublicId && !l.IsDeleted, ct);
            if (destino is null) return Falla(ErroresDePagos.ObligacionInexistente());
            if (destino.DebtorPersonId != credito.DebtorPersonId || destino.Id == credito.Id) return Falla(ErroresDePagos.DestinoAjeno());
            var claveDestino = ClaveDeDestino(s.IdempotencyKey);
            if (await db.LoanOperations.AsNoTracking().AnyAsync(o => o.CollectorCode == r.Recaudador.Code && o.IdempotencyKey == claveDestino, ct))
                return Falla(ErroresDePagos.ClaveReusada(claveDestino));
            var previa = await aplicacion.DistribuirAsync(destino, ComprobantesDePago.Modalidad(s.TargetModality!.Value), s.Amount, hoy,
                ComprobantesDePago.Eleccion(s.Choice), null, usaPreferencia: s.SimulationId is null, ct);
            if (previa.IsFailure) return Falla(previa.Error);
            if (s.SimulationId is { } simulacion && simulacion != previa.Value.SimulationId)
                return Falla(new ErrorConDatos(ErroresDePagos.CodigoDistribucionCambiada,
                    "El crédito cambió desde la simulación. Revise la distribución nueva y confirme otra vez.",
                    new Dictionary<string, object?> { ["simulation"] = aplicacion.Simulacion(previa.Value) }));
        }
        if (s.Form == FormaDeResolucion.ApplyAsExtraordinaryPrincipal && !await NacioDeUnAbonoAsync(credito.Id, ct))
            return Falla(ErroresDePagos.ResolucionNoAdmitida());

        var contable = await fechaContable.CalcularAsync(hoy, ct);
        if (contable.IsFailure) return Falla(contable.Error);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Falla(sucursal.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var decision = new DecisionDeResolucion
        {
            Concept = concepto.ToString(), Amount = s.Amount, Form = s.Form.ToString(), Choice = s.Choice?.ToString(),
            TargetObligationPublicId = s.TargetObligationPublicId, TargetModality = s.TargetModality?.ToString(), CollectorCode = r.Recaudador.Code,
            CollectorClass = r.Recaudador.Class.ToString(),
        };
        var payload = decision.Serializar();
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.CreditBalanceResolution, PrefijoDeResolucion, ct);
        var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.CreditBalanceResolution,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = hoy,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = s.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = r.Recaudador.Class.ToString(),
            CollectorCode = r.Recaudador.Code,
            IdempotencyKey = s.IdempotencyKey,
            RequestSha256 = huella,
            PrepaymentChoice = ComprobantesDePago.Eleccion(s.Choice),
            ReceiptNumber = s.Receipt.Number,
            ReceiptDate = s.Receipt.Date,
            ReceiptBranchPublicId = s.Receipt.OfficePublicId,
            ReceiptMeansReference = s.Receipt.MeansReference,
            Reason = s.Reason,
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = estado.RulesVersion,
            PrevalidationOutcome = r.ValidacionPrevia.ToString(),
        });

        var entrada = cargado.Entrada!;
        var id = CargadorDeReconstruccion.IdEnLaEntrada(operacion);
        var conLaResolucion = CargadorDeReconstruccion.ConOperacion(entrada, operacion, decision.Contenido());
        var libro = marca >= hoy ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
        EfectoEnElLibro efecto;
        try
        {
            efecto = EfectoEnElLibro.Calcular(entrada, conLaResolucion, id, hoy, marca, libro, []);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }

        Domain.Entities.Lending.Schedules.RepaymentScheduleVersion? versionDelPlan = null;
        var abono = efecto.Despues.Pagos.GetValueOrDefault(id);
        if (abono?.PlanNuevo is { } plan)
        {
            var numeroDelPlan = (await db.RepaymentScheduleVersions.AsNoTracking().Where(x => x.LoanId == credito.Id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
            versionDelPlan = registro.RegistrarPlanRegenerado(credito.Id, numeroDelPlan, cargado.Vigentes, operacion, plan, entrada.Festivos);
        }

        var datos = (await partes.DatosAsync([cargado], ct))[credito.Id];
        var recaudador = EmisionDeCartera.Recaudador(r.Recaudador, s.IdempotencyKey, ClaseDeMedioDeRecaudo.InternalOffset, datos.DeudorPublicId, s.Receipt);
        var emitidos = await emision.SaldoAFavorResueltoAsync(new ResolucionAEmitir(datos.Hecho(contable.Value), operacion, concepto, s.Amount, s.Form, recaudador,
            null, destino is null ? null : new LoanRefV1 { LoanPublicId = destino.PublicId, LoanNumber = destino.Number }), ct);
        if (emitidos.IsFailure) return Falla(emitidos.Error);
        var mensaje = emitidos.Value.De(SaldoAFavorResueltoV1.Type);
        registro.RegistrarTransacciones(efecto.Propias.Select(f => RegistroDeCartera.FilaDeHecho(credito.Id, f, operacion, null, contable.Value,
            operacion.RegisteredAt, mensaje)));
        registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(credito.Id, efecto.Diferencias, operacion, contable.Value, operacion.RegisteredAt,
            (_, _) => mensaje));
        if (abono is { Cancela: true } && credito.Status is LoanStatus.Active or LoanStatus.WrittenOff && efecto.Despues.ValorParaCancelar <= 0m)
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.PaidOff, hoy);
        await db.SaveChangesAsync(ct);
        if (preparada)
        {
            var alDia = await puestaAlDia.RegistrarAsync(operacion, ct, [credito.Id]);
            if (alDia.IsFailure) return Falla(alDia.Error);
            await db.SaveChangesAsync(ct);
        }

        // La aplicación destino, ya validada antes de escribir: si ahora falla, la transacción entera se aborta (no queda la baja sin su pago).
        ComprobanteDeAplicacionDto? aplicado = null;
        if (destino is not null)
        {
            var pago = await aplicacion.AplicarAsync(new PagoEnCurso(r.Recaudador, s.SimulationId is null ? FormaDeAplicacion.NonInteractive : FormaDeAplicacion.Interactive,
                new PagoAAplicar(ClaveDeDestino(s.IdempotencyKey), destino.PublicId, s.TargetModality!.Value, s.Amount, hoy, ClaseDeMedioDeRecaudo.InternalOffset,
                    datos.DeudorPublicId, s.Receipt, s.Choice, null, s.SimulationId), r.ValidacionPrevia) { Origen = operacion }, ct);
            if (pago.IsFailure)
                throw new InvalidOperationException($"La aplicación destino de la resolución {operacion.Number} falló después de validarse: {pago.Error.Code}: {pago.Error.Message}");
            aplicado = pago.Value.Comprobante;
        }
        else if (abono is not null)
        {
            aplicado = new ComprobanteDeAplicacionDto(operacion.PublicId, operacion.Number, credito.PublicId, credito.Number, s.IdempotencyKey,
                ModalidadDePago.ExtraordinaryPrincipal, hoy, contable.Value, operacion.RegisteredAt, s.Amount, abono.Aplicado, abono.Anticipo, abono.SaldoAFavor,
                abono.Excedente, ComprobantesDePago.Lineas(abono), ComprobantesDePago.Saldos(efecto.Despues), ComprobantesDePago.Plan(abono.PlanNuevo, hoy),
                credito.Status == LoanStatus.PaidOff, null, operacion.InputsSha256, operacion.RulesVersion, abono.Avisos);
        }

        var comprobante = new ComprobanteDeResolucionDto(operacion.PublicId, operacion.Number, credito.PublicId, s.Concept, s.Form, s.Amount, aplicado,
            ComprobantesDePago.Saldos(efecto.Despues));
        return Result.Success(new ResolucionRegistrada(operacion, comprobante, emitidos.Value, false));
    }

    /// <summary>¿El saldo a favor nació de un abono extraordinario sin elección ni preferencia (D24)?</summary>
    private async Task<bool> NacioDeUnAbonoAsync(int loanId, CancellationToken ct)
    {
        var pagos = await db.LoanOperations.AsNoTracking()
            .Where(o => o.LoanId == loanId && o.Kind == LoanOperationKind.Payment && o.PaymentModality == PaymentModality.ExtraordinaryPrincipal
                        && o.ReversedByOperationId == null)
            .Select(o => o.Id).ToListAsync(ct);
        return pagos.Count > 0 && await db.LoanTransactions.AsNoTracking()
            .AnyAsync(t => t.OperationId != null && pagos.Contains(t.OperationId.Value) && t.Kind == LoanTransactionKind.CreditBalanceRecognized, ct);
    }

    /// <summary>El comprobante de una resolución ya registrada.</summary>
    private async Task<ComprobanteDeResolucionDto> ComprobanteAsync(LoanOperation operacion, Loan credito, CancellationToken ct)
    {
        var d = DecisionDeResolucion.Leer(operacion.Payload);
        var hoy = reloj.HoyLocal;
        var cargado = await cargador.CargarAsync(credito.Id, null, hoy, ct);
        var saldos = cargado is { Listo: true }
            ? ComprobantesDePago.Saldos(MotorDeCartera.Reconstruir(cargado.Entrada!, hoy))
            : new SaldosDeObligacionDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0);
        var concepto = d is null ? ConceptoDeObligacion.CreditBalance : ComprobantesDePago.Concepto(Enum.Parse<LoanConcept>(d.Concept));
        var forma = d is null ? FormaDeResolucion.RefundThroughCollector : Enum.Parse<FormaDeResolucion>(d.Form);
        return new ComprobanteDeResolucionDto(operacion.PublicId, operacion.Number, credito.PublicId, concepto, forma, operacion.Amount, null, saldos);
    }

    private static Result<ResolucionRegistrada> Falla(Error e) => Result.Failure<ResolucionRegistrada>(e);
}
