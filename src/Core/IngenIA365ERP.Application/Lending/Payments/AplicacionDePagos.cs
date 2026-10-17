using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>La distribución de un pago sobre el estado a su fecha valor, con su huella y, en un abono sin elección, sus dos alternativas. (nuevo)</summary>
public sealed record PagoDistribuido(
    Loan Credito,
    CreditoCargado Cargado,
    EstadoDelCredito Estado,
    DistribucionDePago Distribucion,
    string SimulationId,
    IReadOnlyList<(DistribucionDePago Distribucion, string SimulationId)> Alternativas,
    bool EleccionDePreferencia);

/// <summary>Un pago que el contrato aplica: quién recauda, la forma, el ítem y la validación previa del lote. (nuevo)</summary>
public sealed record PagoEnCurso(Recaudador Recaudador, FormaDeAplicacion Forma, PagoAAplicar Pago, PrevalidationOutcome ValidacionPrevia)
{
    /// <summary>La resolución de saldo que origina esta aplicación (<c>{clave}:destino</c>, §3.9).</summary>
    public LoanOperation? Origen { get; init; }

    /// <summary>Avisos del lote que van en el comprobante (<c>Lending.Prevalidation.*</c>).</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];
}

/// <summary>Un pago aplicado (o ya aplicado con la misma clave): su operación, el comprobante y los mensajes de su unidad. (nuevo)</summary>
public sealed record PagoAplicado(LoanOperation Operacion, ComprobanteDeAplicacionDto Comprobante, UnidadEmitida Mensajes, bool YaAplicado);

/// <summary>Quien avisa al deudor que un abono sin interacción usó su preferencia (D24, quickstart §3.12). (nuevo)</summary>
public interface IAvisoDeAbonoConPreferencia
{
    Task AvisarAsync(Loan credito, LoanOperation pago, decimal aplicado, PrepaymentChoice eleccion, Domain.Lending.Schedules.PlanDePagos? plan, CancellationToken ct);
}

/// <summary>
/// El <b>núcleo de escritura de los pagos</b> (feature 013, US5, T0542; FR-071–FR-082, T6, T21, T23, T31–T34, C04, D4–D8, D23, D24; interfaces §3.3,
/// §3.7, §3.10): lo usan <c>AplicarAsync</c> del contrato, la aplicación destino de <c>ResolverSaldoAFavorAsync</c> y la recogida de créditos propios
/// del desembolso (<c>OwnLoanPayoff</c>). Corre <b>bajo el cerrojo que ya tomó quien llama</b> y en su transacción; cada ítem, en orden:
/// <list type="number">
/// <item>idempotencia por <c>(CollectorCode, IdempotencyKey)</c> con <see cref="HuellaDeOperacion"/> de <c>Lending.ObligationContract.Aplicar</c>
/// sin <c>SimulationId</c> (§3.10): misma huella → el comprobante original (<c>AlreadyApplied</c>); otra → <c>Lending.Contract.KeyReused</c>;</item>
/// <item>la obligación paga (<c>ObligationNotPayable</c>), la clase de medio (<see cref="ClasesDeMedioDeRecaudo"/>) y la fecha valor
/// (<see cref="ReglasDeFechaValor"/>);</item>
/// <item>la puesta al día del crédito hasta la fecha valor (<see cref="PuestaAlDiaDeCredito"/>, C04), salvo que otra operación de la misma transacción
/// ya la tenga preparada (la recogida dentro de su desembolso);</item>
/// <item>la reconstrucción a la fecha valor y <see cref="AplicadorDePagos.Distribuir"/> —la misma función que simula (FR-072)— con la prelación
/// sellada; un abono sin elección en la forma no interactiva toma la preferencia vigente (<see cref="PreferenciaDePrepagoVigente"/>) y, sin ella,
/// queda como saldo a favor (D24); en la forma interactiva se compara la huella (<c>Lending.Payment.DistributionChanged</c> con
/// <c>data.simulation</c>, sin escribir, T32);</item>
/// <item>el tope legal del prepago sin penalidad (<see cref="TopeLegalDePrepago"/>, FR-075) con el SMMLV y <c>PREPAGO_TOPE_SMMLV</c> a la fecha del
/// desembolso por <see cref="IValoresNacionales"/>: el motor no cobra cargos de prepago, así que sólo se sella (en la decisión, sin columna nueva);
/// la recogida (<c>OwnLoanPayoff</c>) queda fuera del tope y no se mide (decisión del dueño, 2026-10-14);</item>
/// <item>por <see cref="RegistroDeCartera"/>: la operación <c>Payment</c> numerada (<see cref="NumeradorDeCartera"/>, al final del orden de cerrojos y
/// en el orden del lote), sus filas (las del libro teórico con la operación), las diferencias por familia en los días ya causados
/// (<see cref="EfectoEnElLibro"/>), el plan regenerado y el estado <c>PaidOff</c>; la reclamación al seguro que el pago de la aseguradora cierra
/// (T102);</item>
/// <item>la emisión de <c>PagoAplicado</c> y sus acompañantes (<see cref="EmisionDeCartera.PagoAplicadoAsync"/>), el guardado (decide la transacción
/// de afuera), la puesta al día preparada y la posición del día.</item>
/// </list>
/// Un rechazo de negocio es un <see cref="Result"/> fallido: quien llama lo traduce a <c>Rejected</c>. (nuevo)
/// </summary>
public sealed class AplicacionDePagos(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    PuestaAlDiaDeCredito puestaAlDia,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    PartesDeHechosDeCartera partes,
    FechaContable fechaContable,
    FirmezaDelPago firmeza,
    ReglasDeFechaValor fechas,
    PreferenciaDePrepagoVigente preferencias,
    IValoresNacionales valores,
    IActorActual actorActual,
    IAvisoDeAbonoConPreferencia aviso,
    IDateTimeService reloj)
{
    public const string PrefijoDePago = "PAG-";

    /// <summary>El método del contrato en la huella de la clave (§3.10).</summary>
    public const string MetodoAplicar = "Lending.ObligationContract.Aplicar";

    private static readonly HashSet<LoanStatus> NoPagables = [LoanStatus.PendingDisbursement, LoanStatus.PaidOff, LoanStatus.Voided, LoanStatus.Novated];

    /// <summary>La huella de un ítem (sin <c>SimulationId</c>: después de un «cambió» la clave sigue libre).</summary>
    public static string Huella(Recaudador recaudador, FormaDeAplicacion forma, PagoAAplicar pago) =>
        HuellaDeOperacion.Calcular(MetodoAplicar, new
        {
            collector = recaudador.Code,
            collectorClass = recaudador.Class.ToString(),
            form = forma.ToString(),
            payment = pago with { SimulationId = null },
        });

    /// <summary>
    /// Distribuye un pago sobre el estado a <paramref name="fechaValor"/> sin escribir (lo usan la simulación del contrato, la de pantalla y la
    /// aplicación). Un abono extraordinario sin elección en la forma no interactiva toma la preferencia vigente; en la interactiva y al simular
    /// devuelve las dos alternativas.
    /// </summary>
    public async Task<Result<PagoDistribuido>> DistribuirAsync(Loan credito, PaymentModality modalidad, decimal valor, DateOnly fechaValor,
        PrepaymentChoice? eleccion, int? cuota, bool usaPreferencia, CancellationToken ct, DateOnly? seriesHasta = null, DateTime? informacionHasta = null)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var cargado = await cargador.CargarAsync(credito.Id, null, seriesHasta is { } h && h > fechaValor ? h : fechaValor, ct);
        if (cargado is null) return Result.Failure<PagoDistribuido>(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        if (!cargado.Listo) return Result.Failure<PagoDistribuido>(cargado.Error!);
        var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fechaValor);
        if (estado.Plan is null) return Result.Failure<PagoDistribuido>(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        var condiciones = cargado.Entrada!.Condiciones;

        var dePreferencia = false;
        if (modalidad == PaymentModality.ExtraordinaryPrincipal && eleccion is null && usaPreferencia)
        {
            eleccion = await preferencias.EleccionDelCreditoAsync(credito.Id, informacionHasta ?? reloj.UtcNow, ct);
            dePreferencia = eleccion is not null;
        }
        if (valor <= 0m && modalidad is PaymentModality.FullPayoff or PaymentModality.PastDue)
            valor = modalidad == PaymentModality.FullPayoff ? Math.Max(0m, estado.ValorParaCancelar) : ComprobantesDePago.Exigible(estado);
        if (valor <= 0m)
            return Result.Failure<PagoDistribuido>(ErroresDePagos.Validacion([("amount", "GreaterThan", "El valor del pago es mayor que cero.")]));

        var solicitud = new SolicitudDeDistribucion(modalidad, valor, fechaValor) { Eleccion = eleccion, Cuota = cuota };
        if (AplicadorDePagos.Problema(estado, solicitud, condiciones.Prelacion) is { } problema)
            return Result.Failure<PagoDistribuido>(ErroresDeGobierno.De(problema));
        try
        {
            var d = MotorDeCartera.DistribuirPago(estado, solicitud, condiciones.Prelacion, condiciones.Redondeo);
            var huella = ComprobantesDePago.Huella(credito.PublicId, estado.RulesVersion, d);
            var alternativas = modalidad == PaymentModality.ExtraordinaryPrincipal && eleccion is null && d.SaldoAFavor > 0m
                ? MotorDeCartera.AlternativasDeAbono(estado, solicitud, condiciones.Prelacion, condiciones.Redondeo)
                    .Select(a => (a, ComprobantesDePago.Huella(credito.PublicId, estado.RulesVersion, a))).ToList()
                : [];
            return Result.Success(new PagoDistribuido(credito, cargado, estado, d, huella, alternativas, dePreferencia));
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<PagoDistribuido>(ErroresDeGobierno.De(ex.Problema));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<PagoDistribuido>(ErroresDePagos.Validacion([("amount", "Invalid", ex.Message)]));
        }
    }

    /// <summary>La simulación de un pago distribuido (el estado después: la reconstrucción con el pago a la fecha valor).</summary>
    public SimulacionDePagoDto Simulacion(PagoDistribuido p, IReadOnlyList<string>? avisosExtra = null)
    {
        ArgumentNullException.ThrowIfNull(p);
        var alternativas = p.Alternativas.Select(a => ComprobantesDePago.Simulacion(p.Credito.PublicId, a.Distribucion, a.SimulationId,
            Despues(p, a.Distribucion), [], a.Distribucion.Avisos)).ToList();
        return ComprobantesDePago.Simulacion(p.Credito.PublicId, p.Distribucion, p.SimulationId, Despues(p, p.Distribucion), alternativas,
            [.. p.Distribucion.Avisos, .. avisosExtra ?? []]);
    }

    /// <summary>El estado a la fecha valor con el pago ya aplicado (sin escribir), para mostrar los saldos que quedan.</summary>
    private EstadoDelCredito Despues(PagoDistribuido p, DistribucionDePago d)
    {
        var simulado = new LoanOperation
        {
            LoanId = p.Credito.Id, Kind = LoanOperationKind.Payment, ValueDate = d.FechaValor, RegisteredAt = reloj.UtcNow,
            Sequence = int.MaxValue,
        };
        var entrada = CargadorDeReconstruccion.ConOperacion(p.Cargado.Entrada!, simulado,
            new ContenidoDePago(d.Modalidad, d.Valor - d.Excedente) { Eleccion = d.Eleccion, Cuota = d.Cuota });
        return MotorDeCartera.Reconstruir(entrada, d.FechaValor);
    }

    /// <summary>Aplica un pago (ver la clase). El crédito y su control ya están bloqueados por quien llama.</summary>
    public async Task<Result<PagoAplicado>> AplicarAsync(PagoEnCurso p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var pago = p.Pago;
        var r = p.Recaudador;
        var huella = Huella(r, p.Forma, pago);

        var credito = await db.Loans.FirstOrDefaultAsync(l => l.PublicId == pago.ObligationPublicId && !l.IsDeleted, ct);
        if (credito is null) return Falla(ErroresDePagos.ObligacionInexistente());

        // (8) Idempotencia, bajo el cerrojo.
        var existente = await db.LoanOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.CollectorCode == r.Code && o.IdempotencyKey == pago.IdempotencyKey, ct);
        if (existente is not null)
            return existente.RequestSha256 == huella && existente.LoanId == credito.Id
                ? Result.Success(new PagoAplicado(existente, await ComprobanteAsync(existente, credito, ct), UnidadEmitida.Vacia, true))
                : Falla(ErroresDePagos.ClaveReusada(pago.IdempotencyKey));

        if (NoPagables.Contains(credito.Status)) return Falla(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        if (ClasesDeMedioDeRecaudo.Validar(r.Class, pago.MeansClass, esResolucion: p.Origen is not null) is { } medio) return Falla(medio);
        var pagador = await db.People.AsNoTracking().Where(x => x.PublicId == pago.PayerPublicId && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        if (pagador is null) return Falla(new Error(ErroresDePagos.CodigoNoEncontrado, "El pagador no es una persona del maestro."));

        // K2 (T0840; interfaces §3.7, A9): el giro de una pagaduría externa cita la línea «descontado pendiente de giro» que cubre si el crédito tiene
        // una de esa pagaduría; con ella la fecha valor es la del descuento, sin el límite de días ni la atribución antes de un cierre.
        Domain.Entities.Lending.Payroll.PayrollDeductionResult? giro = null;
        if (pago.MeansClass == ClaseDeMedioDeRecaudo.ExternalPaymasterDeduction)
        {
            var referido = await GiroDePagaduriaAsync(credito, pagador.Value, pago.DeductionFileLinePublicId, ct);
            if (referido.IsFailure) return Falla(referido.Error);
            giro = referido.Value;
        }
        if (await fechas.ValidarAsync(r.Class, giro?.DeductionDate ?? pago.ValueDate, pago.Amount, ct, conReferenciaDePagaduria: giro is not null) is { } fecha)
            return Falla(fecha);
        var modalidad = ComprobantesDePago.Modalidad(pago.Modality);
        if (credito.Status == LoanStatus.WrittenOff && modalidad == PaymentModality.Advance)
            return Falla(new ErrorConDatos(ErroresDePagos.CodigoModalidadNoAdmitida, "Un crédito castigado no recibe anticipos: aplique el pago como valor libre.",
                new { modality = modalidad.ToString() }));
        // (9) Puesta al día hasta la fecha valor (la de un desembolso en curso la escribe su tramo).
        var v = giro?.DeductionDate ?? pago.ValueDate;
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
        var deOtro = puestaAlDia.EstaPendiente(credito.Id);
        var preparada = false;
        if (!deOtro && control.LastAccruedDate < v)
        {
            await puestaAlDia.PrepararAsync([credito.Id], v, ct);
            preparada = puestaAlDia.EstaPendiente(credito.Id);
        }
        var marca = control.LastAccruedDate;

        // (10) Reconstruye y distribuye; en la forma interactiva, la huella.
        if (modalidad == PaymentModality.ExtraordinaryPrincipal && pago.Choice is null && p.Forma == FormaDeAplicacion.Interactive)
            return Falla(ErroresDePagos.PreferenciaFaltante());
        var distribuido = await DistribuirAsync(credito, modalidad, pago.Amount, v, ComprobantesDePago.Eleccion(pago.Choice), pago.InstallmentNumber,
            usaPreferencia: p.Forma == FormaDeAplicacion.NonInteractive, ct, seriesHasta: marca,
            informacionHasta: control.LastRegisteredAt >= reloj.UtcNow ? control.LastRegisteredAt.AddTicks(1) : reloj.UtcNow);
        if (distribuido.IsFailure) return Falla(distribuido.Error);
        var d = distribuido.Value;
        if (p.Forma == FormaDeAplicacion.Interactive && !string.Equals(pago.SimulationId, d.SimulationId, StringComparison.Ordinal))
            return Falla(new ErrorConDatos(ErroresDePagos.CodigoDistribucionCambiada,
                "El crédito cambió desde la simulación. Revise la distribución nueva y confirme otra vez.",
                new Dictionary<string, object?> { ["simulation"] = Simulacion(d) }));
        var dist = d.Distribucion;

        // El tope legal del prepago sin penalidad (FR-075): sólo se sella, el motor no cobra cargos de prepago. La recogida queda fuera (decisión del
        // dueño, 2026-10-14): recoger un crédito al desembolsar otro no es un prepago del deudor y no se bloquea por falta del SMMLV o del límite.
        // K6b (T1053): con las reglas de vivienda el prepago va siempre sin penalidad (Ley 546 de 1999): se sella «dentro del tope» sin leer el SMMLV.
        var tope = r.Class == ClaseDeRecaudador.OwnLoanPayoff ? Result.Success<TopeSellado?>(null)
            : Domain.Lending.VersionesDeReglas.EsDeVivienda(d.Estado.RulesVersion) && (modalidad is PaymentModality.ExtraordinaryPrincipal or PaymentModality.FullPayoff || dist.AbonoACapital != 0m)
                ? Result.Success<TopeSellado?>(new TopeSellado(new ResultadoDeTopeDePrepago(true, 0m, 0m), null, null))
                : await TopeAsync(credito, modalidad, dist, ct);
        if (tope.IsFailure) return Falla(tope.Error);

        var datos = (await partes.DatosAsync([d.Cargado], ct))[credito.Id];
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Falla(sucursal.Error);
        var contable = await fechaContable.CalcularAsync(v, ct);
        if (contable.IsFailure) return Falla(contable.Error);
        var firme = await firmeza.CalcularAsync(pago.MeansClass, v, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var reclamacion = await db.InsuranceClaims.FirstOrDefaultAsync(c => c.LoanId == credito.Id && c.Status == InsuranceClaimStatus.InProcess
                                                                            && c.InsurerPersonId == pagador.Value && !c.IsDeleted, ct);

        // (11) La operación (12: numerada al final del orden de cerrojos, en el orden del lote).
        var decision = new DecisionDePago
        {
            Modality = modalidad.ToString(), Received = pago.Amount, Applied = dist.Aplicado, ValueDate = v, Choice = dist.Eleccion?.ToString(),
            ChoiceFromPreference = d.EleccionDePreferencia, Installment = pago.InstallmentNumber, CollectorCode = r.Code, CollectorClass = r.Class.ToString(),
            Form = p.Forma.ToString(), MeansClass = pago.MeansClass.ToString(), SimulationId = p.Forma == FormaDeAplicacion.Interactive ? d.SimulationId : null,
            WithinLegalPrepaymentCap = tope.Value?.Resultado.DentroDelTope, LegalPrepaymentCap = tope.Value?.Resultado.TopeEnPesos,
            MinimumWagePublicId = tope.Value?.Smmlv, CapLimitPublicId = tope.Value?.Limite, SourceOperationPublicId = p.Origen?.PublicId,
        };
        var payload = decision.Serializar();
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.Payment, PrefijoDePago, ct);
        var ahora = reloj.UtcNow;
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.Payment,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = v,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = pago.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = r.Class.ToString(),
            CollectorCode = r.Code,
            IdempotencyKey = pago.IdempotencyKey,
            RequestSha256 = huella,
            PaymentModality = modalidad,
            PrepaymentChoice = dist.Eleccion,
            ChoiceFromPreference = modalidad == PaymentModality.ExtraordinaryPrincipal ? d.EleccionDePreferencia : null,
            PaymentMeansClass = pago.MeansClass.ToString(),
            PayerPersonId = pagador.Value,
            ReceiptNumber = pago.Receipt.Number,
            ReceiptDate = pago.Receipt.Date,
            ReceiptBranchPublicId = pago.Receipt.OfficePublicId,
            ReceiptMeansReference = pago.Receipt.MeansReference,
            SimulationId = decision.SimulationId,
            ExcessAmount = dist.Excedente,
            FinalityDate = firme,
            RelatedOperationId = p.Origen?.Id,
            InsuranceClaimId = reclamacion?.Id,
            PayrollDeductionResultId = giro?.Id,
            PaymasterPendingLineReference = giro is null ? null : pago.DeductionFileLinePublicId?.ToString("D"),
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = d.Estado.RulesVersion,
            PrevalidationOutcome = p.ValidacionPrevia.ToString(),
        });

        // Las filas propias y lo que cambia en los días ya causados (T33).
        var id = CargadorDeReconstruccion.IdEnLaEntrada(operacion);
        var entrada = d.Cargado.Entrada!;
        var conElPago = CargadorDeReconstruccion.ConOperacion(entrada, operacion,
            new ContenidoDePago(modalidad, dist.Aplicado) { Eleccion = dist.Eleccion, Cuota = pago.InstallmentNumber });
        var libro = marca >= v ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
        var efecto = EfectoEnElLibro.Calcular(entrada, conElPago, id, v, marca, libro, []);
        var aplicada = efecto.Despues.Pagos.GetValueOrDefault(id) ?? dist;
        var cancela = aplicada.Cancela && Math.Max(0m, efecto.Despues.ValorParaCancelar) == 0m;

        // El plan regenerado por el abono (FR-075).
        Domain.Entities.Lending.Schedules.RepaymentScheduleVersion? versionDelPlan = null;
        if (aplicada.PlanNuevo is { } planNuevo)
        {
            var numeroDelPlan = (await db.RepaymentScheduleVersions.AsNoTracking().Where(x => x.LoanId == credito.Id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
            versionDelPlan = registro.RegistrarPlanRegenerado(credito.Id, numeroDelPlan, d.Cargado.Vigentes, operacion, planNuevo, entrada.Festivos);
        }

        // (13) Los hechos, y las filas con el mensaje que las lleva.
        var reasignaciones = await ReasignacionesAsync(credito, efecto.Reasignaciones, ct);
        var emitidos = await emision.PagoAplicadoAsync(new PagoAEmitir(datos.Hecho(contable.Value), operacion, r, pago.IdempotencyKey, pago.MeansClass,
            pago.PayerPublicId, pago.Receipt, modalidad, pago.Amount, dist.Excedente, efecto.Propias, efecto.DeCausacion, reasignaciones, aplicada.Eleccion,
            ComprobantesDePago.PlanDelHecho(aplicada.PlanNuevo, versionDelPlan?.VersionNumber ?? 0, d.Cargado.Vigentes.Periodicity.ToString()),
            cancela, cancela ? firme : null, decision.SimulationId, p.ValidacionPrevia, reclamacion?.PublicId)
        {
            DeductionFileLinePublicId = giro is null ? null : pago.DeductionFileLinePublicId,
        }, ct);
        if (emitidos.IsFailure) return Falla(emitidos.Error);
        var u = emitidos.Value;
        if (efecto.Reanuda)
            await emision.ReanudacionPorPagoAsync(datos.Hecho(contable.Value), operacion, EmisionDeCartera.ClasePago, efecto.Despues.AlturaDeMora.Dias, ct);
        registro.RegistrarTransacciones(efecto.Propias.Select(f => RegistroDeCartera.FilaDeHecho(credito.Id, f, operacion, null, contable.Value, operacion.RegisteredAt,
            MensajeDeFila(u, f.Clase, f.Concepto))));
        registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(credito.Id, efecto.Diferencias, operacion, contable.Value, operacion.RegisteredAt,
            (_, clase) => clase == LoanTransactionKind.PaymentReallocation ? u.De(PagoAplicadoV1.Type) : u.De(AjusteDeCausacionPorFechaValorV1.Type)));

        if (cancela && credito.Status is LoanStatus.Active or LoanStatus.WrittenOff)
            RegistroDeCartera.CambiarEstado(credito, LoanStatus.PaidOff, v);
        await db.SaveChangesAsync(ct);

        // La reclamación al seguro que paga la aseguradora (T102), la puesta al día preparada (con la operación ya guardada) y la posición.
        if (reclamacion is not null)
        {
            reclamacion.Status = InsuranceClaimStatus.Paid;
            reclamacion.PaidOperationId = operacion.Id;
            reclamacion.PaidAmount = (reclamacion.PaidAmount ?? 0m) + aplicada.Aplicado;
            reclamacion.ClosedAt = ahora;
            reclamacion.CloseReason = $"Pagada con la operación {operacion.Number}";
        }
        if (preparada)
        {
            var alDia = await puestaAlDia.RegistrarAsync(operacion, ct, [credito.Id]);
            if (alDia.IsFailure) return Falla(alDia.Error);
        }
        if (!deOtro)
        {
            var dia = efecto.Hasta;
            var ultimas = await PosicionesDeCartera.UltimasAsync(db, [credito.Id], dia, dia, ct);
            var ultima = ultimas.GetValueOrDefault((credito.Id, dia));
            var local = db.LoanDailyPositions.Local.Where(x => x.LoanId == credito.Id && x.PositionDate == dia).Select(x => x.Version).DefaultIfEmpty(0).Max();
            registro.PosicionVersionada(control, efecto.Despues, credito, d.Cargado.Vigentes, Math.Max(ultima.Version, local), ultima.Huella, ahora,
                credito.Status);
        }
        await db.SaveChangesAsync(ct);

        if (d.EleccionDePreferencia && aplicada.Eleccion is { } usada)
            await aviso.AvisarAsync(credito, operacion, aplicada.Aplicado, usada, aplicada.PlanNuevo, ct);

        var avisos = new List<string>(aplicada.Avisos);
        avisos.AddRange(p.Avisos);
        var comprobante = new ComprobanteDeAplicacionDto(operacion.PublicId, operacion.Number, credito.PublicId, credito.Number, pago.IdempotencyKey,
            pago.Modality, v, contable.Value, operacion.RegisteredAt, pago.Amount, dist.Aplicado, aplicada.Anticipo, aplicada.SaldoAFavor,
            dist.Excedente, ComprobantesDePago.Lineas(aplicada), ComprobantesDePago.Saldos(efecto.Despues),
            ComprobantesDePago.Plan(aplicada.PlanNuevo, v), cancela, cancela ? firme : null, operacion.InputsSha256, operacion.RulesVersion,
            avisos.Distinct().ToList());
        return Result.Success(new PagoAplicado(operacion, comprobante, u, false));
    }

    /// <summary>El comprobante de un pago ya registrado (la respuesta <c>AlreadyApplied</c>, o la consulta de la operación).</summary>
    public async Task<ComprobanteDeAplicacionDto> ComprobanteAsync(LoanOperation operacion, Loan credito, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        var filas = await db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == operacion.Id && t.ReversesTransactionId == null).ToListAsync(ct);
        var lineas = filas.Where(t => t.Kind is LoanTransactionKind.PaymentAllocation or LoanTransactionKind.Recovery
                                      || (t.Kind == LoanTransactionKind.AdvanceApplied && t.Concept == LoanConcept.Advance))
            .GroupBy(t => (t.InstallmentNumber, t.Concept))
            .Select(g => new LineaDeDistribucionDto(g.Key.InstallmentNumber, ComprobantesDePago.Concepto(g.Key.Concept), -g.Sum(t => t.Amount)))
            .ToList();
        var marca = await db.LoanControls.AsNoTracking().Where(c => c.LoanId == credito.Id).Select(c => c.LastAccruedDate).FirstAsync(ct);
        var fecha = marca > operacion.ValueDate ? marca : operacion.ValueDate;
        var cargado = await cargador.CargarAsync(credito.Id, null, fecha, ct);
        var saldos = cargado is { Listo: true }
            ? ComprobantesDePago.Saldos(MotorDeCartera.Reconstruir(cargado.Entrada!, fecha))
            : new SaldosDeObligacionDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0);
        var decision = DecisionDePago.Leer(operacion.Payload);
        var excedente = operacion.ExcessAmount ?? 0m;
        var pagado = credito.Status == LoanStatus.PaidOff;
        return new ComprobanteDeAplicacionDto(operacion.PublicId, operacion.Number, credito.PublicId, credito.Number, operacion.IdempotencyKey ?? string.Empty,
            ComprobantesDePago.Modalidad(operacion.PaymentModality ?? PaymentModality.FreeAmount), operacion.ValueDate, operacion.AccountingDate,
            operacion.RegisteredAt, operacion.Amount, operacion.Amount - excedente,
            filas.Where(t => t.Kind == LoanTransactionKind.AdvanceReceived).Sum(t => t.Amount),
            filas.Where(t => t.Kind == LoanTransactionKind.CreditBalanceRecognized).Sum(t => t.Amount),
            excedente, lineas, saldos, null, pagado, pagado ? operacion.FinalityDate : null, operacion.InputsSha256, operacion.RulesVersion,
            decision?.Choice is null && operacion.PaymentModality == PaymentModality.ExtraordinaryPrincipal ? [AplicadorDePagos.AvisoPreferenciaAsumida] : []);
    }

    /// <summary>Los pagos redistribuidos, como <c>reallocations</c> del hecho (con su <c>PagoAplicado</c> original).</summary>
    public async Task<IReadOnlyList<ReallocationV1>> ReasignacionesAsync(Loan credito, IReadOnlyList<ReasignacionDePago> reasignaciones, CancellationToken ct)
    {
        if (reasignaciones.Count == 0) return [];
        var ids = reasignaciones.Select(x => long.TryParse(x.OperacionId, out var n) ? n : 0L).Where(n => n != 0).ToList();
        var operaciones = await db.LoanOperations.AsNoTracking().Where(o => ids.Contains(o.Id)).Select(o => new { o.Id, o.PublicId, o.Number }).ToListAsync(ct);
        var salida = new List<ReallocationV1>();
        foreach (var x in reasignaciones)
        {
            var op = operaciones.FirstOrDefault(o => CargadorDeReconstruccion.Id(o.Id) == x.OperacionId);
            if (op is null) continue;
            var original = await emision.HechoOriginalAsync(op.PublicId, PagoAplicadoV1.Type, op.Number, ct)
                ?? new OriginalFactRefV1 { Type = PagoAplicadoV1.Type, OriginPublicId = op.PublicId, Number = op.Number };
            salida.Add(new ReallocationV1 { Payment = original, Amounts = ComprobantesDePago.Reasignacion(x.Antes, x.Despues) });
        }
        return salida;
    }

    /// <summary>Los números de los pagos redistribuidos (<c>ReallocatedPaymentNumbers</c>).</summary>
    public async Task<IReadOnlyList<string>> NumerosAsync(IReadOnlyList<ReasignacionDePago> reasignaciones, CancellationToken ct)
    {
        var ids = reasignaciones.Select(x => long.TryParse(x.OperacionId, out var n) ? n : 0L).Where(n => n != 0).ToList();
        if (ids.Count == 0) return [];
        return await db.LoanOperations.AsNoTracking().Where(o => ids.Contains(o.Id)).OrderBy(o => o.Id).Select(o => o.Number).ToListAsync(ct);
    }

    /// <summary>El mensaje que lleva una fila de un pago (mensajes.md §7.2).</summary>
    public static Guid? MensajeDeFila(UnidadEmitida u, LoanTransactionKind clase, LoanConcept concepto) => clase switch
    {
        LoanTransactionKind.AdvanceReceived => u.De(AnticipoRecibidoV1.Type),
        LoanTransactionKind.CreditBalanceRecognized => u.De(SaldoAFavorReconocidoV1.Type),
        LoanTransactionKind.Recovery => u.De(RecuperacionDeCastigadaV1.Type) ?? u.De(PagoAplicadoV1.Type),
        LoanTransactionKind.PaymentAllocation when concepto is LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense =>
            u.De(CobroDeInteresEnSuspensoV1.Type) ?? u.De(PagoAplicadoV1.Type),
        _ => u.De(PagoAplicadoV1.Type),
    };

    /// <summary>
    /// K2 (T0840; interfaces §3.7; data-model §24.8): lo descontado pendiente de giro del crédito por esta pagaduría (el pagador del pago). Sin
    /// pendientes de ella, el pago va sin referencia y no levanta nada (nulo; una referencia que llegue igual es <c>PaymasterReferenceInvalid</c>);
    /// con pendientes, la referencia es obligatoria (<c>Lending.Payment.PaymasterReferenceRequired</c>) y tiene que ser de uno de ellos
    /// (<c>.PaymasterReferenceInvalid</c>).
    /// </summary>
    private async Task<Result<Domain.Entities.Lending.Payroll.PayrollDeductionResult?>> GiroDePagaduriaAsync(Loan credito, int pagador, Guid? linea,
        CancellationToken ct)
    {
        var pendientes = await (from res in Payroll.PendientesDeGiro.Consulta(db).AsNoTracking()
                                join f in db.PayrollDeductionFiles.AsNoTracking() on res.PayrollDeductionFileId equals f.Id
                                join a in db.PayrollAgreements.AsNoTracking() on f.PayrollAgreementId equals a.Id
                                where res.LoanId == credito.Id && a.PaymasterPersonId == pagador
                                select res).ToListAsync(ct);
        if (linea is null)
            return pendientes.Count > 0
                ? Result.Failure<Domain.Entities.Lending.Payroll.PayrollDeductionResult?>(Payroll.ErroresDeLibranza.ReferenciaRequerida(credito.Number))
                : Result.Success<Domain.Entities.Lending.Payroll.PayrollDeductionResult?>(null);
        var id = await db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PublicId == linea.Value).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        var pendiente = pendientes.Where(x => x.PayrollDeductionFileLineId == id).OrderByDescending(x => x.Id).FirstOrDefault();
        return pendiente is null || pendiente.DeductionDate is null
            ? Result.Failure<Domain.Entities.Lending.Payroll.PayrollDeductionResult?>(Payroll.ErroresDeLibranza.ReferenciaInvalida(credito.Number))
            : Result.Success<Domain.Entities.Lending.Payroll.PayrollDeductionResult?>(pendiente);
    }

    private sealed record TopeSellado(ResultadoDeTopeDePrepago Resultado, Guid? Smmlv, Guid? Limite);

    /// <summary>
    /// El tope legal del prepago (FR-075) en un abono extraordinario a capital o en una cancelación: el SMMLV y el límite a la fecha del desembolso.
    /// Nulo si la modalidad no es un prepago; <c>NationalValues.Missing</c> si falta un valor nacional. La recogida (<c>OwnLoanPayoff</c>) no llega
    /// aquí: queda fuera del tope (decisión del dueño, 2026-10-14).
    /// </summary>
    private async Task<Result<TopeSellado?>> TopeAsync(Loan credito, PaymentModality modalidad, DistribucionDePago d, CancellationToken ct)
    {
        if (modalidad is not (PaymentModality.ExtraordinaryPrincipal or PaymentModality.FullPayoff) && d.AbonoACapital == 0m)
            return Result.Success<TopeSellado?>(null);
        var smmlv = await valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, credito.OpenedOn, null, ct);
        if (smmlv.IsFailure) return Result.Failure<TopeSellado?>(smmlv.Error);
        var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PrepagoTopeSmmlv, credito.OpenedOn, null, ct);
        if (limite.IsFailure) return Result.Failure<TopeSellado?>(limite.Error);
        if (smmlv.Value.Valor is not { } salario || limite.Value.Valor is not { } veces || credito.ApprovedAmount <= 0m)
            return Result.Success<TopeSellado?>(null);
        return Result.Success<TopeSellado?>(new TopeSellado(TopeLegalDePrepago.Evaluar(credito.ApprovedAmount, salario, veces), smmlv.Value.PublicId,
            limite.Value.PublicId));
    }

    private static Result<PagoAplicado> Falla(Error e) => Result.Failure<PagoAplicado>(e);
}
