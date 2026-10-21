using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>La simulación del plan de una venta (la del simulador de la línea) y si la venta quedó fuera de los rangos de la línea. (nuevo)</summary>
public sealed record SimulacionDeVenta(SimulacionCalculada Simulacion, bool FueraDeRango);

/// <summary>Lo que hace falta para que nazca la obligación de una venta (o de un reemplazo, C17). (nuevo)</summary>
public sealed record PedidoDeObligacionDeVenta(
    VentaConvertida Venta,
    SimulacionCalculada Simulacion,
    EvaluacionDeVenta Evaluacion,
    LendingInboundMessage Recibo,
    PrevalidationOutcome ValidacionPrevia,
    Loan? Reemplaza = null);

/// <summary>La obligación nacida: el crédito, su desembolso en especie, de dónde salió el número, la reserva usada, el plan y los mensajes. (nuevo)</summary>
public sealed record ObligacionNacida(
    Loan Credito,
    LoanOperation Desembolso,
    LoanTermsVersion Condiciones,
    string FuenteDelNumero,
    LoanNumberReservation? Reserva,
    bool ReservaNoEncontrada,
    LoanPlanSummaryV1 Plan,
    Guid SucursalPublicId,
    DateOnly FechaContable,
    IReadOnlyList<IntegrationMessage> Mensajes);

/// <summary>
/// El nacimiento de la obligación de una venta a crédito (feature 013, US6, T0590; FR-050, FR-052, FR-056, FR-057, C20, C21, C62, T22, T34, T56;
/// mensajes.md §15.3 pasos 5 a 8). <b>Crea siempre la obligación</b> (FR-050), bajo el cerrojo del deudor que ya tomó quien llama:
/// <list type="number">
/// <item>el número: el reservado de la v2 (<c>Reserved</c> o <c>Expired</c> → <c>Used</c>; una vencida alerta <c>Cartera.ReservaVencida</c>, C21) o, si
/// la reserva no existe o no coincide, uno nuevo del numerador al final del orden de cerrojos (<c>numberSource = Assigned</c>, C20);</item>
/// <item>el crédito con <c>Origin = Commercial</c>, la factura, el pago, el mensaje y quién registró la cuenta por cobrar (FR-056), y
/// <c>SaleObligationStatus</c> (validada o con validación fallida con su código y motivo, FR-052); su control;</item>
/// <item>por <see cref="RegistroDeCartera"/>: la operación <c>Disbursement</c> <c>InKind</c> con su fila <c>+Principal</c> y el recibo
/// (<c>InboundMessageId</c>), la versión de condiciones <c>CommercialSale</c>, el plan y la posición del día;</item>
/// <item>la preferencia de abono heredada del cupo rotativo (<c>InheritedFromLimit</c>, FR-049);</item>
/// <item>con validación fallida, el bloqueo <c>ValidationFailed</c> en <c>LND_DebtorBlocks</c>; la marca no cambia causación, cobro ni calificación;</item>
/// <item><c>CreditoDesembolsado</c> en especie y, en el mismo guardado, los cobros provisionales de esa factura (<c>LND_ProvisionalCollections</c>)
/// como operaciones <c>ProvisionalCollection</c> con su fecha valor, sin recaudador y en firme, cada uno con su <c>CobroProvisionalIncorporado</c>, y
/// su aplicación escrita una sola vez (C62).</item>
/// </list>
/// Guarda dentro de la transacción de quien llama (los Id del crédito y de la operación); no confirma. (nuevo)
/// </summary>
public sealed class NacimientoDeObligacionDeVenta(
    IApplicationDbContext db,
    SimuladorDeCredito simulador,
    TramoDeDesembolso tramos,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    CargadorDeReconstruccion cargador,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    DisponibleDeCupo cupos,
    ReglasDeParametrosDeCartera parametros,
    IAlertas alertas,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string PrefijoDeCobroProvisional = "CPV-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// El plan de la venta con el simulador de la línea a la fecha de la venta: la tasa de la banda que casa, la usura del día y el primer vencimiento
    /// de Comercial. Fuera de los rangos de la línea no impide: se simula sin ellos y lo dice <see cref="SimulacionDeVenta.FueraDeRango"/> (lo evalúa
    /// FR-051). Una línea cerrada o sin la usura del día sí falla.
    /// </summary>
    public async Task<Result<SimulacionDeVenta>> SimularAsync(VentaConvertida c, decimal monto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var t = c.Venta.Condiciones;
        var fecha = c.Venta.Fecha;
        var pedido = new ScheduleSimulationInput(c.Linea.PublicId, monto, c.Cuotas, c.Periodicidad, DisbursementDate: fecha,
            FirstDueDate: t.FirstDueDate > fecha ? t.FirstDueDate : null, PersonPublicId: c.Persona.PublicId);
        var perfil = new PerfilParaLaBanda(c.TipoDeDeudor, c.Persona.IsEmployee, null);
        var conRangos = await simulador.SimularAsync(c.Linea, pedido, perfil, fecha, validarRangos: true, ct);
        if (conRangos.IsSuccess) return Result.Success(new SimulacionDeVenta(conRangos.Value, false));
        if (conRangos.Error.Code != ErroresDeOriginacion.CodigoFueraDeRango) return Result.Failure<SimulacionDeVenta>(conRangos.Error);
        var sinRangos = await simulador.SimularAsync(c.Linea, pedido, perfil, fecha, validarRangos: false, ct);
        return sinRangos.IsSuccess ? Result.Success(new SimulacionDeVenta(sinRangos.Value, true)) : Result.Failure<SimulacionDeVenta>(sinRangos.Error);
    }

    public async Task<Result<ObligacionNacida>> NacerAsync(PedidoDeObligacionDeVenta p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var c = p.Venta;
        var v = c.Venta;
        var fecha = v.Fecha;
        var monto = p.Simulacion.Plan.TotalCapital;
        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        var usuario = v.Mensaje.Envelope.OriginUser;

        // La oficina: la sucursal de la venta (o la principal); su sucursal contable va en el sobre de los hechos (T13).
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == v.Mensaje.Envelope.BranchPublicId && !b.IsDeleted)
                          .Select(b => (int?)b.Id).FirstOrDefaultAsync(ct)
                      ?? await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is not { } oficinaId)
            return Result.Failure<ObligacionNacida>(new Error(ErroresDeDesembolso.CodigoSinSucursalContable, "La cooperativa no tiene oficinas registradas."));
        var sucursal = await emision.SucursalContableAsync(oficinaId, ct);
        if (sucursal.IsFailure) return Result.Failure<ObligacionNacida>(sucursal.Error);
        var contable = await fechaContable.CalcularAsync(fecha, ct);
        if (contable.IsFailure) return Result.Failure<ObligacionNacida>(contable.Error);

        // El cupo que la venta consume (el rotativo de la línea o el general).
        var cupo = await cupos.ParaVentaAsync(c.Persona.Id, c.Linea.Id, fecha, ct, sinElPago: v.Pago.PaymentPublicId);

        // El número: el reservado (C20, C21) o uno nuevo, al final del orden de cerrojos.
        var reserva = await ReservaUsableAsync(v, c.Persona.Id, ct);
        var falla = p.Evaluacion.Falla;
        var credito = new Loan
        {
            DebtorPersonId = c.Persona.Id,
            DebtorKind = c.TipoDeDeudor,
            BranchId = oficinaId,
            CreditLineId = c.Linea.Id,
            Origin = LoanOrigin.Commercial,
            Status = LoanStatus.Active,
            ApprovedAmount = monto,
            OpenedOn = fecha,
            CreditLimitId = cupo is { } k && k.Cupo.Id > 0 && k.Cupo.Kind == CreditLimitKind.Revolving ? k.Cupo.Id : null,
            SourceSalesDocumentPublicId = v.DocumentoPublicId,
            SourcePaymentPublicId = v.Pago.PaymentPublicId,
            SourceInvoiceNumber = Recortar(v.NumeroDeFactura, 30),
            SourceMessagePublicId = v.Mensaje.MessageId,
            AccountsReceivableRecordedBy = v.AccountsReceivableRecordedBy,
            SaleObligationStatus = falla is null ? SaleObligationStatus.Validated : SaleObligationStatus.ValidationFailed,
            ValidationFailureCode = falla?.Code,
            ValidationFailureReason = falla?.Reason,
            OriginLoanId = p.Reemplaza?.Id,
            OriginLinkKind = p.Reemplaza is null ? null : LoanLinkKind.Replacement,
        };
        if (reserva is not null)
            NumeradorDeCartera.NumerarCreditoConSuReserva(credito, reserva);
        else
            await numerador.NumerarCreditoAsync(credito, TramoDeDesembolso.PrefijoDeCredito, ct);
        db.Loans.Add(credito);
        var control = registro.IniciarControl(credito, fecha, ahora);

        // La operación Disbursement en especie con su fila +Principal y el recibo.
        var entradas = JsonSerializer.Serialize(new
        {
            sale = v.DocumentoPublicId, invoice = v.NumeroDeFactura, payment = v.Pago.PaymentPublicId, message = v.Mensaje.MessageId, amount = monto,
            line = c.Linea.Code, periodicity = c.Periodicidad.ToString(), installments = c.Cuotas, reservation = reserva?.PublicId,
            accountsReceivableRecordedBy = v.AccountsReceivableRecordedBy, replaces = p.Reemplaza?.PublicId, simulation = p.Simulacion.Huella,
        }, Json);
        var numeroDeOperacion = await numerador.NumeroDeOperacionAsync(LoanOperationKind.Disbursement, TramoDeDesembolso.PrefijoDeDesembolso, ct);
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new LoanOperation
        {
            Loan = credito,
            Kind = LoanOperationKind.Disbursement,
            Number = numeroDeOperacion,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = monto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = usuario.CentralUserId ?? actor.CentralUserId,
            DisbursementForm = DisbursementForm.InKind,
            TrancheNumber = 1,
            InboundMessageId = p.Recibo.Id == 0 ? null : p.Recibo.Id,
            Payload = entradas,
            InputsSha256 = TramoDeDesembolso.Sha256(entradas),
            RulesVersion = p.Simulacion.Plan.RulesVersion,
            PrevalidationOutcome = p.ValidacionPrevia.ToString(),
        });
        registro.RegistrarTransacciones(
        [
            new LoanTransaction
            {
                Loan = credito,
                Operation = operacion,
                Kind = LoanTransactionKind.Disbursement,
                Concept = LoanConcept.Principal,
                Amount = monto,
                ValueDate = fecha,
                AccountingDate = contable.Value,
                RegisteredAt = operacion.RegisteredAt,
            },
        ]);
        var moraSobreIntereses = (await parametros.LeerAsync(ParametrosDeCartera.MoraSobreIntereses, fecha, ct: ct)) is { IsSuccess: true } m
                                 && m.Value.Valor is true
                                 && !string.Equals(c.Version.Version.ModalityCode, "VIVIENDA", StringComparison.OrdinalIgnoreCase);
        var condiciones = tramos.Condiciones(credito, operacion, p.Simulacion, fecha, moraSobreIntereses, TermsVersionCause.CommercialSale);
        await tramos.PlanAsync(credito, condiciones, operacion, fecha, monto, p.Simulacion.Huella, 1, p.Simulacion.Plan, ct);
        registro.RegistrarPosicion(control, Posicion(credito, fecha, monto, operacion, condiciones));

        if (reserva is not null)
        {
            if (reserva.Status == LoanNumberReservationStatus.Expired)
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ReservaVencida, "Venta con una reserva de número vencida",
                    $"La venta {v.NumeroDeFactura} llegó con el número {reserva.Number}, cuya reserva había vencido: el crédito lo toma igual.",
                    EntityType: nameof(LoanNumberReservation), EntityPublicId: reserva.PublicId,
                    DedupKey: $"{TiposDeAlerta.ReservaVencida}:{reserva.PublicId:D}:usada", ModuloDeOrigen: "Lending"), ct);
            reserva.Status = LoanNumberReservationStatus.Used;
            reserva.UsedAt = ahora;
        }
        await db.SaveChangesAsync(ct);
        if (reserva is not null) reserva.LoanId = credito.Id;

        // La preferencia de abono heredada del cupo rotativo (FR-049).
        if (await PreferenciaDelCupoAsync(credito.CreditLimitId, ahora, ct) is { } eleccion)
            db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(c.Persona.Id, credito.Id, null, eleccion, 1, operacion.RegisteredAt,
                PreferenceOrigin.InheritedFromLimit, actor.UserId, null, operacion.Id));

        // La marca de validación fallida bloquea crédito nuevo al deudor (FR-052).
        if (falla is not null)
            db.DebtorBlocks.Add(DebtorBlock.Nuevo(c.Persona.Id, DebtorBlockReason.ValidationFailed, ClaveDelBloqueo(credito.PublicId), fecha,
                $"Venta {v.NumeroDeFactura}: {falla.Reason}", null, credito.Id));

        // CreditoDesembolsado en especie (FR-050, FR-056).
        var hecho = new HechoDeCredito(credito, condiciones, c.Linea.Code, c.Persona.PublicId, c.Persona.IsEmployee, sucursal.Value, contable.Value);
        var plan = TramoDeDesembolso.Resumen(p.Simulacion, 1);
        var enEspecie = new InKindDisbursementV1
        {
            SaleMessageId = v.Mensaje.MessageId,
            Invoice = EmisionDeCartera.Factura(v.Mensaje),
            CreditPaymentPublicId = v.Pago.PaymentPublicId,
            AccountsReceivableRecordedBy = v.AccountsReceivableRecordedBy,
            ReplacesLoan = p.Reemplaza is { } r ? new LoanRefV1 { LoanPublicId = r.PublicId, LoanNumber = r.Number } : null,
        };
        var origen = UsuarioDeOrigen(v.Mensaje);
        var emitido = await emision.DesembolsoEnEspecieAsync(hecho, operacion, monto, plan, enEspecie, p.ValidacionPrevia, origen, ct);
        if (emitido.IsFailure) return Result.Failure<ObligacionNacida>(emitido.Error);
        List<IntegrationMessage> mensajes = [emitido.Value];
        await db.SaveChangesAsync(ct);

        // Los cobros provisionales de esa factura, una sola vez (C62).
        var cobros = await CobrosProvisionalesAsync(credito, control, hecho, v, p.Recibo, actor, origen, ct);
        if (cobros.IsFailure) return Result.Failure<ObligacionNacida>(cobros.Error);
        mensajes.AddRange(cobros.Value);

        return Result.Success(new ObligacionNacida(credito, operacion, condiciones, reserva is null ? EmisionDeCartera.FuenteAsignado : EmisionDeCartera.FuenteReserva,
            reserva, reserva is null && v.ReservationPublicId is not null, plan, sucursal.Value, contable.Value, mensajes));
    }

    /// <summary>La clave del bloqueo automático de una validación fallida (su disparador): el crédito marcado.</summary>
    public static string ClaveDelBloqueo(Guid loanPublicId) => $"ValidationFailed:{loanPublicId:N}";

    /// <summary>El usuario de Comercial que confirmó la venta (T11), como dato.</summary>
    public static UserRefV1? UsuarioDeOrigen(MensajeEntrante m) =>
        m.Envelope.OriginUser.CentralUserId is null ? null : new UserRefV1 { CentralUserId = m.Envelope.OriginUser.CentralUserId, Name = m.Envelope.OriginUser.Name };

    /// <summary>
    /// La reserva de la v2 que se puede usar: la del <c>reservationPublicId</c>, del mismo pago y deudor, con el número que trae el mensaje, en
    /// <c>Reserved</c> o <c>Expired</c>. Si no existe o no coincide, nula: el crédito nace con otro número (C20).
    /// </summary>
    private async Task<LoanNumberReservation?> ReservaUsableAsync(VentaRecibida v, int personId, CancellationToken ct)
    {
        if (v.ReservationPublicId is not { } id || string.IsNullOrWhiteSpace(v.ReservedLoanNumber)) return null;
        var r = await db.LoanNumberReservations.FirstOrDefaultAsync(x => x.PublicId == id && !x.IsDeleted, ct);
        return r is not null && r.Number == v.ReservedLoanNumber && r.PaymentPublicId == v.Pago.PaymentPublicId && r.PersonId == personId
               && r.Status is LoanNumberReservationStatus.Reserved or LoanNumberReservationStatus.Expired
            ? r
            : null;
    }

    /// <summary>La elección de abono vigente del cupo rotativo (de cualquiera de sus versiones), o nula.</summary>
    private async Task<PrepaymentChoice?> PreferenciaDelCupoAsync(int? cupoId, DateTime ahora, CancellationToken ct)
    {
        if (cupoId is not { } id) return null;
        var cupo = await db.CreditLimits.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (cupo is null) return null;
        var versiones = await db.CreditLimits.AsNoTracking()
            .Where(l => l.PersonId == cupo.PersonId && l.Kind == cupo.Kind && l.CreditLineId == cupo.CreditLineId).Select(l => l.Id).ToListAsync(ct);
        var preferencias = await db.PrepaymentPreferences.AsNoTracking().Where(p => p.CreditLimitId != null && versiones.Contains(p.CreditLimitId!.Value))
            .ToListAsync(ct);
        return PrepaymentPreference.Vigente(preferencias, ahora)?.Choice;
    }

    /// <summary>
    /// Los cobros provisionales de la factura todavía sin aplicar (C62): cada uno, una operación <c>ProvisionalCollection</c> (valor libre, en firme,
    /// sin recaudador) con las filas del libro teórico a su fecha valor y su <c>CobroProvisionalIncorporado</c>; la fila del cobro escribe una sola vez
    /// el crédito, la operación, el instante y el mensaje.
    /// </summary>
    private async Task<Result<IReadOnlyList<IntegrationMessage>>> CobrosProvisionalesAsync(Loan credito, LoanControl control, HechoDeCredito hecho,
        VentaRecibida v, LendingInboundMessage recibo, IngenIA365ERP.Application.Common.Execution.Actor actor, UserRefV1? origen, CancellationToken ct)
    {
        var cobros = await db.ProvisionalCollections.Where(x => x.InvoiceNumber == v.NumeroDeFactura && x.LoanId == null && !x.IsDeleted)
            .OrderBy(x => x.ValueDate).ThenBy(x => x.RowNumber).ToListAsync(ct);
        var mensajes = new List<IntegrationMessage>();
        foreach (var cobro in cobros)
        {
            var cargado = await cargador.CargarAsync(credito.Id, null, cobro.ValueDate, ct);
            if (cargado is not { Listo: true }) return Result.Failure<IReadOnlyList<IntegrationMessage>>(cargado?.Error ?? ErroresDePagos.ObligacionInexistente());
            var contable = await fechaContable.CalcularAsync(cobro.ValueDate, ct);
            if (contable.IsFailure) return Result.Failure<IReadOnlyList<IntegrationMessage>>(contable.Error);
            var decision = new DecisionDePago
            {
                Modality = nameof(PaymentModality.FreeAmount), Received = cobro.Amount, Applied = cobro.Amount, ValueDate = cobro.ValueDate,
                CollectorCode = string.Empty, CollectorClass = string.Empty, Form = "ProvisionalCollection", MeansClass = string.Empty,
            };
            var payload = decision.Serializar();
            var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.ProvisionalCollection, PrefijoDeCobroProvisional, ct);
            var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
            {
                LoanId = credito.Id,
                Kind = LoanOperationKind.ProvisionalCollection,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = cobro.ValueDate,
                AccountingDate = contable.Value,
                RegisteredAt = sello.RegisteredAt,
                Amount = cobro.Amount,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = origen?.CentralUserId ?? actor.CentralUserId,
                PaymentModality = PaymentModality.FreeAmount,
                FinalityDate = cobro.ValueDate,
                InboundMessageId = recibo.Id == 0 ? null : recibo.Id,
                Reason = $"Cobro provisional {cobro.AccountingReference}",
                Payload = payload,
                InputsSha256 = TramoDeDesembolso.Sha256(payload),
                RulesVersion = hecho.Condiciones.RulesVersion,
            });
            var id = CargadorDeReconstruccion.IdEnLaEntrada(operacion);
            var conElCobro = CargadorDeReconstruccion.ConOperacion(cargado.Entrada!, operacion, new ContenidoDePago(PaymentModality.FreeAmount, cobro.Amount));
            var estado = MotorDeCartera.Reconstruir(conElCobro, cobro.ValueDate, cobro.ValueDate);
            var propias = estado.Libro.Movimientos.Where(x => x.OperacionId == id).ToList();
            var activacion = await db.LendingActivations.AsNoTracking().Where(a => a.Id == cobro.ActivationId).Select(a => (Guid?)a.PublicId).FirstOrDefaultAsync(ct);
            var emitido = await emision.CobroProvisionalAsync(new CobroProvisionalAEmitir(hecho with { FechaContable = contable.Value }, operacion, propias,
                EmisionDeCartera.Factura(v.Mensaje), v.AccountsReceivableRecordedBy, cobro.AccountingReference, activacion ?? Guid.Empty, cobro.RowNumber, origen), ct);
            if (emitido.IsFailure) return Result.Failure<IReadOnlyList<IntegrationMessage>>(emitido.Error);
            registro.RegistrarTransacciones(propias.Select(f => RegistroDeCartera.FilaDeHecho(credito.Id, f, operacion, null, contable.Value, operacion.RegisteredAt,
                emitido.Value.PublicId)));
            await db.SaveChangesAsync(ct);
            cobro.LoanId = credito.Id;
            cobro.OperationId = operacion.Id;
            cobro.AppliedAt = operacion.RegisteredAt;
            cobro.MessagePublicId = emitido.Value.PublicId;
            await db.SaveChangesAsync(ct);
            mensajes.Add(emitido.Value);
        }
        return Result.Success<IReadOnlyList<IntegrationMessage>>(mensajes);
    }

    /// <summary>La posición del día de la venta: el capital financiado, sin mora (data-model §6.1).</summary>
    private static LoanDailyPosition Posicion(Loan credito, DateOnly fecha, decimal monto, LoanOperation operacion, LoanTermsVersion condiciones) => new()
    {
        Loan = credito,
        PositionDate = fecha,
        Version = 1,
        RegisteredAt = operacion.RegisteredAt,
        Status = LoanStatus.Active,
        BranchId = credito.BranchId,
        CreditLineId = credito.CreditLineId,
        ModalityCode = condiciones.ModalityCode,
        SubsegmentCode = condiciones.SubsegmentCode,
        Principal = monto,
        PayoffAmount = monto,
        AppliedAnnualRate = condiciones.AnnualRate,
        InputsSha256 = operacion.InputsSha256,
    };

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
