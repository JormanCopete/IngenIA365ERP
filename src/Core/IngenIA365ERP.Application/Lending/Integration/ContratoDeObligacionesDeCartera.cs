using System.Reflection;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// ¿Hay una transacción del recaudador en curso? (feature 013, US5, T0547; T91, interfaces §2 «Escrituras» (1), §3.3 paso 3). En producción es
/// <see cref="TransaccionExplicita.HayEnCurso"/> sobre el contexto del ámbito; las pruebas sin contenedores (InMemory no tiene transacciones) la
/// sustituyen. (nuevo)
/// </summary>
public interface ITransaccionDelRecaudador
{
    bool HayEnCurso();
}

/// <summary>La del contexto del ámbito de quien llama. (nuevo)</summary>
public sealed class TransaccionDelRecaudador(IApplicationDbContext db) : ITransaccionDelRecaudador
{
    public bool HayEnCurso() => TransaccionExplicita.HayEnCurso(db);
}

/// <summary>
/// La <b>implementación real</b> del contrato de obligaciones (feature 013, US5, T0547; FR-083–FR-087, T21, T31–T34, T56, T68, T91, T101, A8, A11,
/// I-4; contracts/interfaces.md §3). Reemplaza a <see cref="ContratoDeObligacionesSinActivar"/> en <c>DependencyInjection</c>. Corre en la unidad
/// de trabajo de quien recauda (el <see cref="IApplicationDbContext"/> de su ámbito), se llama directo —nunca por <c>ISender</c>— y nunca lanza por
/// un rechazo de negocio. Las tres escrituras siguen los pasos de §3.3:
/// <list type="number">
/// <item>el intento, por un contexto aparte (<see cref="AuditoriaDeEscriturasDeCartera"/>);</item>
/// <item>su validador FluentValidation (<c>Validation.Invalid</c> con <c>data.errors[] { field, code }</c>);</item>
/// <item>la transacción en curso (<c>Lending.Contract.TransactionRequired</c>; nunca abre ni confirma la suya);</item>
/// <item>la activación (<c>LND_Setup.ActivationDate</c>; sin ella <c>Lending.NotActivated</c>, §3.17);</item>
/// <item>el recaudador en el registro (<see cref="IRegistroDeIntegracion"/>, §3.2; <c>OwnLoanPayoff</c> exento, T91);</item>
/// <item>la validación previa contable fuera del cerrojo, una pregunta por combinación de dimensiones y <b>siempre como aviso</b> (§3.12, A8);</item>
/// <item>el cerrojo de todo el lote en una pasada (<see cref="ICerrojoDeCartera"/>: <c>LND_Setup</c> compartido → deudores → créditos por Id);</item>
/// <item>cada ítem por <see cref="AplicacionDePagos"/>, <see cref="ReversionDePago"/> o <see cref="ResolucionDeSaldoAFavor"/>, y los no descontados
/// por <see cref="NoDescontadosDelLote"/>;</item>
/// <item>el hecho de auditoría de cada ítem en <c>COR_AuditOutbox</c> dentro de la transacción, con la distribución completa.</item>
/// </list>
/// Un rechazo de un ítem no rechaza a los demás; uno de lote se repite en cada ítem (§3.4). Listar y simular no escriben y responden
/// <see cref="ResultadoDelContrato.Answered"/> (C28). (nuevo)
/// </summary>
public sealed class ContratoDeObligacionesDeCartera(
    IApplicationDbContext db,
    ITransaccionDelRecaudador transaccion,
    IRegistroDeIntegracion registroDeIntegracion,
    ValidacionPreviaContableDeCartera validacionPrevia,
    ICerrojoDeCartera cerrojo,
    CargadorDeReconstruccion cargador,
    AplicacionDePagos aplicacion,
    ReversionDePago reversion,
    ResolucionDeSaldoAFavor resolucion,
    NoDescontadosDelLote registroDeNoDescontados,
    ReglasDeFechaValor fechas,
    AuditoriaDeEscriturasDeCartera auditoria,
    IValidator<SolicitudDeAplicacion> validadorDeAplicacion,
    IValidator<SolicitudDeReverso> validadorDeReverso,
    IValidator<SolicitudDeResolucion> validadorDeResolucion,
    IValidator<SolicitudDeSimulacion> validadorDeSimulacion,
    IDateTimeService reloj) : IContratoDeObligaciones
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string MensajeAplicado = "Pago aplicado.";
    public const string MensajeYaAplicado = "El pago ya estaba aplicado con esta clave: se devuelve el comprobante original.";
    public const string MensajeReversado = "Pago reversado.";
    public const string MensajeResuelto = "Saldo resuelto.";

    // ------------------------------------------------------------------------------------------------------------ listar --

    public async Task<RespuestaDelContratoDto<ListadoDeObligacionesDto>> ListarAsync(SolicitudDeListado solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!await ActivadaAsync(ct))
            return new(ResultadoDelContrato.Answered, ErroresDePagos.CodigoNoActivada, ContratoDeObligacionesSinActivar.MensajeNoActivada,
                new ListadoDeObligacionesDto(EstadoDeCartera.NotActivated, solicitud.PersonPublicId, solicitud.Date, []));
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta) return Rechazo<ListadoDeObligacionesDto>(noCuenta);
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == solicitud.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Rechazo<ListadoDeObligacionesDto>(new Error(ErroresDePagos.CodigoNoEncontrado, "La persona no existe en el maestro."));

        var fecha = solicitud.Date;
        var comoDeudor = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == persona && !l.IsDeleted
            && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff || l.Status == LoanStatus.PaidOff)).ToListAsync(ct);
        var comoCodeudor = solicitud.IncludeAsCoDebtor
            ? await (from p in db.LoanParties.AsNoTracking()
                     join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                     where p.PersonId == persona && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted && p.ReleasedOn == null && p.ExtinguishedOn == null
                           && !l.IsDeleted && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff)
                     select l).ToListAsync(ct)
            : [];
        var creditos = comoDeudor.Select(l => (Credito: l, Rol: RolEnLaObligacion.Debtor))
            .Concat(comoCodeudor.Where(c => comoDeudor.All(d => d.Id != c.Id)).Select(l => (Credito: l, Rol: RolEnLaObligacion.CoDebtor)))
            .ToList();
        var cargados = await cargador.CargarAsync(creditos.Select(c => c.Credito.Id).ToList(), null, fecha, ct);
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Select(c => c.Credito.CreditLineId).Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => (l.Code, l.Name), ct);
        var enInsolvencia = await db.InsolvencyProceedings.AsNoTracking()
            .AnyAsync(i => i.PersonId == persona && i.Status == InsolvencyProceedingStatus.Negotiating && !i.IsDeleted, ct);

        var obligaciones = new List<ObligacionListadaDto>();
        foreach (var (credito, rol) in creditos.OrderBy(c => c.Credito.Number, StringComparer.Ordinal))
        {
            if (!cargados.TryGetValue(credito.Id, out var cargado) || !cargado.Listo) continue;
            var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fecha);
            if (estado.Plan is null) continue;
            if (credito.Status == LoanStatus.PaidOff && estado.Anticipo <= 0m && estado.SaldoAFavor <= 0m) continue;
            var admitidas = Enum.GetValues<ModalidadDePago>()
                .Where(m => m != ModalidadDePago.SpecificInstallment || cargado.Entrada!.Condiciones.Prelacion.CuotaEspecificaFueraDeOrden)
                .Where(m => m != ModalidadDePago.Advance || credito.Status != LoanStatus.WrittenOff)
                .Where(_ => credito.Status != LoanStatus.PaidOff)
                .ToList();
            var bloqueos = new List<string>();
            if (credito.Status == LoanStatus.WrittenOff) bloqueos.Add("WrittenOff");
            if (enInsolvencia) bloqueos.Add("Insolvency");
            var linea = lineas.GetValueOrDefault(credito.CreditLineId);
            obligaciones.Add(new ObligacionListadaDto(credito.PublicId, credito.Number, linea.Code ?? string.Empty, linea.Name ?? string.Empty, rol,
                ComprobantesDePago.Estado(credito.Status), estado.AlturaDeMora.Dias, ComprobantesDePago.Exigible(estado), ComprobantesDePago.CuotaDelCiclo(estado),
                ComprobantesDePago.Exigible(estado, incluyeElDia: false), Math.Max(0m, estado.ValorParaCancelar), estado.Anticipo, estado.SaldoAFavor,
                admitidas, bloqueos, HasPaymentAgreement: false, credito.SaleObligationStatus == SaleObligationStatus.ValidationFailed));
        }
        var listado = new ListadoDeObligacionesDto(EstadoDeCartera.Active, solicitud.PersonPublicId, fecha, obligaciones);
        await auditoria.LecturaAsync(AuditEventTypes.LendingObligationContractListed, "Person", solicitud.PersonPublicId, solicitud.Collector,
            new { date = fecha, recordedAsOf = reloj.UtcNow, count = obligaciones.Count, sha256 = HuellaDeOperacion.Calcular("Listed", listado) }, ct);
        return new(ResultadoDelContrato.Answered, AuditEventTypes.LendingObligationContractListed, "Obligaciones listadas.", listado);
    }

    // ------------------------------------------------------------------------------------------------------------ simular --

    public async Task<RespuestaDelContratoDto<SimulacionDePagoDto>> SimularAsync(SolicitudDeSimulacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var forma = await validadorDeSimulacion.ValidateAsync(solicitud, ct);
        if (!forma.IsValid) return Rechazo<SimulacionDePagoDto>(Invalida(forma));
        if (!await ActivadaAsync(ct))
            return new(ResultadoDelContrato.Answered, ErroresDePagos.CodigoNoActivada, ContratoDeObligacionesSinActivar.MensajeNoActivada, null);
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta) return Rechazo<SimulacionDePagoDto>(noCuenta);

        SimulacionDePagoDto simulacion;
        if (solicitud.PaymentIdempotencyKeyToReverse is { } clave)
        {
            var pago = await db.LoanOperations.AsNoTracking().FirstOrDefaultAsync(o => o.CollectorCode == solicitud.Collector.Code && o.IdempotencyKey == clave, ct);
            if (pago is null) return Rechazo<SimulacionDePagoDto>(ErroresDePagos.PagoInexistente());
            var vista = await reversion.PrevisualizarAsync(pago, ct);
            if (vista.IsFailure) return Rechazo<SimulacionDePagoDto>(vista.Error);
            var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == pago.LoanId, ct);
            var v = vista.Value;
            var lineas = v.PorConcepto.Select(kv => new LineaDeDistribucionDto(null, ComprobantesDePago.Concepto(kv.Key), kv.Value)).Concat(v.Ajustes).ToList();
            simulacion = new SimulacionDePagoDto(v.Huella, credito.PublicId, ComprobantesDePago.Modalidad(pago.PaymentModality ?? PaymentModality.FreeAmount),
                pago.Amount, pago.ValueDate, null, -(pago.Amount - (pago.ExcessAmount ?? 0m)), 0m, 0m, 0m, lineas, v.SaldosDespues, v.PlanDespues, [],
                v.Redistribuidos.Count > 0 ? ["Lending.Reversal.ReallocatesPayments"] : []);
        }
        else
        {
            var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == solicitud.ObligationPublicId && !l.IsDeleted, ct);
            if (credito is null) return Rechazo<SimulacionDePagoDto>(ErroresDePagos.ObligacionInexistente());
            if (credito.Status is LoanStatus.PendingDisbursement or LoanStatus.PaidOff or LoanStatus.Voided or LoanStatus.Novated)
                return Rechazo<SimulacionDePagoDto>(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
            if (await fechas.ValidarAsync(solicitud.Collector.Class, solicitud.ValueDate, solicitud.Amount, ct) is { } fecha)
                return Rechazo<SimulacionDePagoDto>(fecha);
            var d = await aplicacion.DistribuirAsync(credito, ComprobantesDePago.Modalidad(solicitud.Modality), solicitud.Amount, solicitud.ValueDate,
                ComprobantesDePago.Eleccion(solicitud.Choice), solicitud.InstallmentNumber, usaPreferencia: false, ct);
            if (d.IsFailure) return Rechazo<SimulacionDePagoDto>(d.Error);
            var avisos = d.Value.Estado.PrimerDiaSinUsura is not null ? new[] { "Lending.Usury.ProjectedWithLastKnown" } : [];
            simulacion = aplicacion.Simulacion(d.Value, avisos);
        }
        await auditoria.LecturaAsync(AuditEventTypes.LendingObligationContractSimulated, "Loan", simulacion.ObligationPublicId, solicitud.Collector,
            new { date = simulacion.ValueDate, recordedAsOf = reloj.UtcNow, sha256 = simulacion.SimulationId, modality = simulacion.Modality.ToString() }, ct);
        return new(ResultadoDelContrato.Answered, AuditEventTypes.LendingObligationContractSimulated, "Pago simulado.", simulacion);
    }

    // ------------------------------------------------------------------------------------------------------------ aplicar --

    public async Task<RespuestaDeAplicacionDto> AplicarAsync(SolicitudDeAplicacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var pagos = solicitud.Payments ?? [];
        var noDescontados = solicitud.NotDeducted ?? [];
        var claves = pagos.Select(p => p.IdempotencyKey).Concat(noDescontados.Select(n => n.IdempotencyKey)).Distinct().ToList();
        await auditoria.IntentoAsync(AuditEventTypes.LendingObligationContractAttempted, nameof(AplicarAsync), solicitud.Collector, claves,
            pagos.Count + noDescontados.Count, pagos.Sum(p => p.Amount), HuellaDeOperacion.Calcular(AplicacionDePagos.MetodoAplicar, solicitud), ct);

        if (await RechazoDeLoteAsync(validadorDeAplicacion, solicitud, solicitud.Collector, ct) is { } lote)
            return await RechazarLoteAsync(solicitud, lote, ct);

        var validacion = await ValidacionPreviaAsync(solicitud.Collector, pagos, ct);
        var avisos = validacion switch
        {
            PrevalidationOutcome.NotPostable => new[] { "Lending.Prevalidation.NotPostable" },
            PrevalidationOutcome.NoResponse => ["Lending.Prevalidation.NoResponse"],
            _ => [],
        };

        await BloquearAsync(pagos.Select(p => p.ObligationPublicId).Concat(noDescontados.Select(n => n.ObligationPublicId)).ToList(), ct);

        var respuestas = new List<RespuestaDelContratoDto<ComprobanteDeAplicacionDto>>(pagos.Count);
        foreach (var pago in pagos)
        {
            var r = await aplicacion.AplicarAsync(new PagoEnCurso(solicitud.Collector, solicitud.Form, pago, validacion) { Avisos = avisos }, ct);
            if (r.IsSuccess)
            {
                var c = r.Value.Comprobante;
                await auditoria.HechoAsync(AuditEventTypes.LendingObligationContractApplied, pago.IdempotencyKey, solicitud.Collector, nameof(AplicarAsync),
                    new { operation = c.Number, obligation = c.ObligationNumber, valueDate = c.ValueDate, amount = c.Amount, applied = c.AppliedAmount,
                          advance = c.AdvanceAmount, creditBalance = c.CreditBalanceAmount, surplus = c.Surplus, distribution = c.Distribution,
                          alreadyApplied = r.Value.YaAplicado }, ct);
                respuestas.Add(new(r.Value.YaAplicado ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied,
                    AuditEventTypes.LendingObligationContractApplied, r.Value.YaAplicado ? MensajeYaAplicado : MensajeAplicado, c));
            }
            else
            {
                await auditoria.HechoAsync(AuditEventTypes.LendingObligationContractRejected, pago.IdempotencyKey, solicitud.Collector, nameof(AplicarAsync),
                    new { code = r.Error.Code, message = r.Error.Message, obligation = pago.ObligationPublicId, amount = pago.Amount }, ct);
                respuestas.Add(Rechazo<ComprobanteDeAplicacionDto>(r.Error));
            }
        }

        var registrados = new List<RespuestaDelContratoDto<NoDescontadoRegistradoDto>>(noDescontados.Count);
        foreach (var n in noDescontados)
        {
            var r = await registroDeNoDescontados.RegistrarAsync(solicitud.Collector, n, ct);
            await auditoria.HechoAsync(r.Result == ResultadoDelContrato.Rejected ? AuditEventTypes.LendingObligationContractRejected : AuditEventTypes.LendingObligationContractApplied,
                n.IdempotencyKey, solicitud.Collector, nameof(AplicarAsync),
                new { notDeducted = n.NotDeductedAmount, expected = n.ExpectedAmount, reason = n.Reason.ToString(), installment = r.Value?.InstallmentNumber,
                      sourceItemPublicId = n.SourceItemPublicId, code = r.Code }, ct);
            registrados.Add(r);
        }
        await db.SaveChangesAsync(ct);
        return new RespuestaDeAplicacionDto(respuestas, registrados);
    }

    // ------------------------------------------------------------------------------------------------------------ reversar --

    public async Task<RespuestaDelContratoDto<ComprobanteDeReversoDto>> ReversarAsync(SolicitudDeReverso solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingObligationContractAttempted, nameof(ReversarAsync), solicitud.Collector,
            [solicitud.PaymentIdempotencyKey], 1, 0m, HuellaDeOperacion.Calcular(ReversionDePago.MetodoReversar, solicitud), ct);
        if (await RechazoDeLoteAsync(validadorDeReverso, solicitud, solicitud.Collector, ct) is { } lote)
            return await RechazoAuditadoAsync<ComprobanteDeReversoDto>(lote, solicitud.Collector, solicitud.PaymentIdempotencyKey, nameof(ReversarAsync), ct);

        var pago = await db.LoanOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.CollectorCode == solicitud.Collector.Code && o.IdempotencyKey == solicitud.PaymentIdempotencyKey, ct);
        if (pago is null)
            return await RechazoAuditadoAsync<ComprobanteDeReversoDto>(ErroresDePagos.PagoInexistente(), solicitud.Collector, solicitud.PaymentIdempotencyKey,
                nameof(ReversarAsync), ct);
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == pago.LoanId, ct);
        await BloquearAsync([credito.PublicId], ct);

        var r = await reversion.ReversarAsync(new ReversoEnCurso(pago, solicitud.Reason, EmisionDeCartera.IniciadoPorElRecaudador,
            ReversionDePago.ClaveDeReverso(solicitud.PaymentIdempotencyKey), solicitud.Receipt), ct);
        if (r.IsFailure)
            return await RechazoAuditadoAsync<ComprobanteDeReversoDto>(r.Error, solicitud.Collector, solicitud.PaymentIdempotencyKey, nameof(ReversarAsync), ct);
        var c = r.Value.Comprobante;
        await auditoria.HechoAsync(AuditEventTypes.LendingObligationContractReversed, solicitud.PaymentIdempotencyKey, solicitud.Collector, nameof(ReversarAsync),
            new { operation = c.Number, reversed = c.ReversedNumber, obligation = c.ObligationNumber, amount = c.ReversedAmount, reason = c.Reason,
                  adjustments = c.Adjustments, reallocated = c.ReallocatedPaymentNumbers, reopened = c.ObligationReopened }, ct);
        await db.SaveChangesAsync(ct);
        return new(r.Value.YaReversado ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied, AuditEventTypes.LendingObligationContractReversed,
            MensajeReversado, c);
    }

    // ------------------------------------------------------------------------------------------------------------ resolver --

    public async Task<RespuestaDelContratoDto<ComprobanteDeResolucionDto>> ResolverSaldoAFavorAsync(SolicitudDeResolucion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingObligationContractAttempted, nameof(ResolverSaldoAFavorAsync), solicitud.Collector,
            [solicitud.IdempotencyKey], 1, solicitud.Amount, HuellaDeOperacion.Calcular(ResolucionDeSaldoAFavor.MetodoResolver, solicitud), ct);
        if (await RechazoDeLoteAsync(validadorDeResolucion, solicitud, solicitud.Collector, ct) is { } lote)
            return await RechazoAuditadoAsync<ComprobanteDeResolucionDto>(lote, solicitud.Collector, solicitud.IdempotencyKey, nameof(ResolverSaldoAFavorAsync), ct);

        await BloquearAsync(solicitud.TargetObligationPublicId is { } destino ? [solicitud.ObligationPublicId, destino] : [solicitud.ObligationPublicId], ct);
        var validacion = PrevalidationOutcome.NotApplicable;
        var r = await resolucion.ResolverAsync(new ResolucionEnCurso(solicitud.Collector, solicitud, validacion), ct);
        if (r.IsFailure)
            return await RechazoAuditadoAsync<ComprobanteDeResolucionDto>(r.Error, solicitud.Collector, solicitud.IdempotencyKey, nameof(ResolverSaldoAFavorAsync), ct);
        var c = r.Value.Comprobante;
        await auditoria.HechoAsync(AuditEventTypes.LendingObligationContractCreditBalanceResolved, solicitud.IdempotencyKey, solicitud.Collector,
            nameof(ResolverSaldoAFavorAsync),
            new { operation = c.Number, obligation = c.ObligationPublicId, concept = c.Concept.ToString(), form = c.Form.ToString(), amount = c.Amount,
                  target = c.TargetApplication?.Number, distribution = c.TargetApplication?.Distribution }, ct);
        await db.SaveChangesAsync(ct);
        return new(r.Value.YaResuelta ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied,
            AuditEventTypes.LendingObligationContractCreditBalanceResolved, MensajeResuelto, c);
    }

    // ------------------------------------------------------------------------------------------------------------ comunes --

    /// <summary>Los datos de un error con datos, como diccionario de <c>data</c> (camelCase).</summary>
    public static IReadOnlyDictionary<string, object?>? Datos(ErrorConDatos error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Data switch
        {
            IReadOnlyDictionary<string, object?> d => d,
            IDictionary<string, object?> d => new Dictionary<string, object?>(d),
            null => null,
            var o => o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), p => p.GetValue(o)),
        };
    }

    private static RespuestaDelContratoDto<T> Rechazo<T>(Error e) =>
        new(ResultadoDelContrato.Rejected, e.Code, e.Message, default, e is ErrorConDatos d ? Datos(d) : null);

    private async Task<RespuestaDelContratoDto<T>> RechazoAuditadoAsync<T>(Error e, Recaudador recaudador, string clave, string metodo, CancellationToken ct)
    {
        await auditoria.HechoAsync(AuditEventTypes.LendingObligationContractRejected, clave, recaudador, metodo, new { code = e.Code, message = e.Message }, ct);
        await db.SaveChangesAsync(ct);
        return Rechazo<T>(e);
    }

    /// <summary>Los rechazos de lote, en el orden de §3.3: forma, transacción, activación y registro. Nulo si el lote sigue.</summary>
    private async Task<Error?> RechazoDeLoteAsync<T>(IValidator<T> validador, T solicitud, Recaudador? recaudador, CancellationToken ct)
    {
        var forma = await validador.ValidateAsync(solicitud, ct);
        if (!forma.IsValid) return Invalida(forma);
        if (!transaccion.HayEnCurso()) return ErroresDePagos.TransaccionRequerida();
        if (!await ActivadaAsync(ct)) return new Error(ErroresDePagos.CodigoNoActivada, ContratoDeObligacionesSinActivar.MensajeNoActivada);
        return await RecaudadorAsync(recaudador!, ct);
    }

    private async Task<RespuestaDeAplicacionDto> RechazarLoteAsync(SolicitudDeAplicacion solicitud, Error e, CancellationToken ct)
    {
        foreach (var clave in (solicitud.Payments ?? []).Select(p => p.IdempotencyKey).Concat((solicitud.NotDeducted ?? []).Select(n => n.IdempotencyKey)).Distinct())
            await auditoria.HechoAsync(AuditEventTypes.LendingObligationContractRejected, clave ?? string.Empty, solicitud.Collector ?? Recaudador.Nomina,
                nameof(AplicarAsync), new { code = e.Code, message = e.Message }, ct);
        if (e.Code != ErroresDePagos.CodigoTransaccionRequerida) await db.SaveChangesAsync(ct);
        return new RespuestaDeAplicacionDto(
            (solicitud.Payments ?? []).Select(_ => Rechazo<ComprobanteDeAplicacionDto>(e)).ToList(),
            (solicitud.NotDeducted ?? []).Select(_ => Rechazo<NoDescontadoRegistradoDto>(e)).ToList());
    }

    private static Error Invalida(FluentValidation.Results.ValidationResult r) =>
        ErroresDePagos.Validacion(r.Errors.Select(e => (JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName), e.ErrorCode, e.ErrorMessage)).ToList());

    private Task<bool> ActivadaAsync(CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);

    /// <summary>
    /// ¿Cuenta el recaudador? (§3.2): fila activa a hoy con rol <c>ObligationsCollector</c>, su clase, una versión del contrato que este despliegue
    /// soporta y no un doble en producción. <c>OwnLoanPayoff</c> queda exenta: es Cartera en su propio desembolso (T91).
    /// </summary>
    private async Task<Error?> RecaudadorAsync(Recaudador r, CancellationToken ct)
    {
        if (r.Class == ClaseDeRecaudador.OwnLoanPayoff)
            return r.Code == ClavesDeRecaudador.Cartera ? null : ErroresDePagos.RecaudadorNoRegistrado(r.Code);
        var hoy = reloj.HoyLocal;
        var vigente = await registroDeIntegracion.RecaudadorAsync(r.Code, hoy, ct);
        if (vigente is not null)
            return string.Equals(vigente.Class.ToString(), r.Class.ToString(), StringComparison.Ordinal) ? null : ErroresDePagos.RecaudadorNoRegistrado(r.Code);

        var adopciones = await (from p in db.IntegrationParticipants.AsNoTracking()
                                join a in db.IntegrationParticipantAdoptions.AsNoTracking() on p.Id equals a.ParticipantId
                                where p.Code == r.Code && p.Role == IntegrationParticipantRole.ObligationsCollector && p.IsEnabled && !p.IsDeleted && !a.IsDeleted
                                      && a.ContractName == ContratosDeIntegracion.ProveedorDeObligaciones
                                select new { p.ValidFrom, p.ValidTo, a.Version, AdoptedFrom = a.ValidFrom, AdoptedTo = a.ValidTo }).ToListAsync(ct);
        var sinSoporte = adopciones.FirstOrDefault(a => a.ValidFrom <= hoy && (a.ValidTo is null || a.ValidTo >= hoy) && a.AdoptedFrom <= hoy
                                                         && (a.AdoptedTo is null || a.AdoptedTo >= hoy)
                                                         && !ContratosDeIntegracion.VersionesDelContratoDeObligaciones.Contains(a.Version));
        return sinSoporte is not null ? ErroresDePagos.VersionNoSoportada(sinSoporte.Version) : ErroresDePagos.RecaudadorNoRegistrado(r.Code);
    }

    /// <summary>
    /// El cerrojo de todo el lote en una pasada (§3.3 paso 7, T34): <c>LND_Setup</c> compartido, los deudores y codeudores de las obligaciones en
    /// exclusivo por Id y los créditos en exclusivo por Id, en una sentencia. Los que la transacción ya tiene (la recogida dentro de su desembolso) no
    /// esperan.
    /// </summary>
    private async Task BloquearAsync(IReadOnlyCollection<Guid> obligaciones, CancellationToken ct)
    {
        var publicos = obligaciones.Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => publicos.Contains(l.PublicId)).Select(l => new { l.Id, l.DebtorPersonId }).ToListAsync(ct);
        if (creditos.Count == 0) return;
        var ids = creditos.Select(c => c.Id).Distinct().Order().ToList();
        var codeudores = await db.LoanParties.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.Role == CreditPartyRole.CoDebtor && !p.IsDeleted)
            .Select(p => p.PersonId).ToListAsync(ct);
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync(creditos.Select(c => c.DebtorPersonId).Concat(codeudores).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
        await cerrojo.BloquearCreditosAsync(ids, ModoDeEspera.Esperar, ct);
    }

    /// <summary>
    /// La validación previa contable del lote (§3.12, A8): un <c>PagoAplicado</c> por combinación de dimensiones (línea, modalidad, sub-segmento,
    /// sucursal y clase del recaudador), fuera del cerrojo; en el contrato nunca bloquea: su sello va en cada operación y su aviso en el comprobante.
    /// </summary>
    private async Task<PrevalidationOutcome> ValidacionPreviaAsync(Recaudador recaudador, IReadOnlyList<PagoAAplicar> pagos, CancellationToken ct)
    {
        if (pagos.Count == 0) return PrevalidationOutcome.NotApplicable;
        var publicos = pagos.Select(p => p.ObligationPublicId).Distinct().ToList();
        var combinaciones = await (from l in db.Loans.AsNoTracking()
                                   join c in db.CreditLines.AsNoTracking().IgnoreQueryFilters() on l.CreditLineId equals c.Id
                                   join t in db.LoanTermsVersions.AsNoTracking() on l.Id equals t.LoanId
                                   join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on l.BranchId equals b.Id
                                   where publicos.Contains(l.PublicId) && t.VersionNumber == 1
                                   select new { c.Code, t.ModalityCode, t.SubsegmentCode, Branch = b.PublicId, l.DebtorPersonId }).Distinct().ToListAsync(ct);
        var hechos = combinaciones.Select(c => (c.Code, c.ModalityCode, c.SubsegmentCode, c.Branch)).Distinct().Select(c => new MensajeContableDto(
            new IntegrationEnvelopeV1
            {
                MessageId = Guid.Empty, Type = PagoAplicadoV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = c.Branch,
            },
            new PagoAplicadoV1
            {
                Dimensions = new LoanFactDimensionsV1
                {
                    CreditLineCode = c.Code, ModalityCode = c.ModalityCode, SubsegmentCode = c.SubsegmentCode,
                    RiskCategory = CategoriaContableDeCartera.CategoriaDeCreditoNuevo, RiskSubcategory = "None", CollectorClass = recaudador.Class.ToString(),
                    BranchPublicId = c.Branch, ImpairmentModel = "ArrearsAnnex1", RiskSegment = c.ModalityCode, ValueUnit = "Pesos",
                },
                Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = 1m }],
            })).ToList();
        var r = await validacionPrevia.EvaluarAsync(hechos, ct);
        return r.Outcome;
    }
}
