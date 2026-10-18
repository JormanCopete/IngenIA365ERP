using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La implementación real de <see cref="IReservasDeCartera"/> (feature 013, US6, T0584; FR-057, FR-058, T21, T22, T56, T68, C24, C25, D26;
/// interfaces.md §5.2; data-model §2.11). Reemplaza a <see cref="ReservasDeCarteraNoHabilitada"/> en <c>DependencyInjection</c>. Corre dentro de la
/// confirmación de Comercial, en su unidad de trabajo, y <b>nunca guarda</b>: confirma Comercial, así que si la venta se revierte la reserva nunca
/// existió. Cada escritura:
/// <list type="number">
/// <item>el intento <c>Lending.NumberReservation.Attempted</c> por un contexto aparte (<see cref="AuditoriaDeEscriturasDeCartera"/>);</item>
/// <item>su validador FluentValidation (<c>Validation.Invalid</c>);</item>
/// <item>Cartera activada (si no, <c>NotEnabled</c>) y la transacción en curso de quien llama (<c>Lending.Contract.TransactionRequired</c>);</item>
/// <item>bajo un <see cref="PuntoDeGuardado"/> propio (C25): una falla técnica vuelve a él y responde <c>NoResponse</c>, y la venta sigue sin número
/// (D26);</item>
/// <item>el orden de cerrojos de la venta a crédito (C24): <c>LND_Setup</c> compartido → <c>LND_DebtorControls</c> del deudor (la fila se crea si
/// falta) → el numerador de Cartera al final; nunca toma cerrojos de Inventario;</item>
/// <item>bajo el cerrojo: la idempotencia por la clave con su huella (misma huella, el mismo número; otra, <c>Lending.Reservation.KeyReused</c>), el
/// estado del deudor (bloqueado o con más mora que <c>Cupo.MoraMaximaParaVender</c>: <c>Lending.Reservation.Blocked</c>) y el cupo
/// (<c>Lending.Reservation.QuotaExceeded</c>); la reserva lleva el valor y <b>descuenta cupo</b> mientras está <c>Reserved</c>;</item>
/// <item>el hecho <c>Lending.NumberReservation.Reserved</c> / <c>.Released</c> en <c>COR_AuditOutbox</c> dentro de la transacción.</item>
/// </list>
/// Sin evaluación contable: la reserva no produce hecho (T56). (nuevo)
/// </summary>
public sealed class ReservasDeCartera(
    IApplicationDbContext db,
    ITransaccionDelRecaudador transaccion,
    ICerrojoDeCartera cerrojo,
    NumeradorDeCartera numerador,
    DisponibleDeCupo cupos,
    IBloqueosDelDeudor bloqueos,
    ReglasDeParametrosDeCartera parametros,
    AuditoriaDeEscriturasDeCartera auditoria,
    IValidator<SolicitudDeReserva> validadorDeReserva,
    IValidator<SolicitudDeLiberacion> validadorDeLiberacion,
    IActorActual actorActual,
    IDateTimeService reloj) : IReservasDeCartera
{
    /// <summary>El punto de guardado propio de la reserva (C25).</summary>
    public const string PuntoDeLaReserva = "lnd_reserva_numero";

    public const string MetodoReservar = "Lending.NumberReservation.Reservar";
    public const string EntidadDeAuditoria = nameof(LoanNumberReservation);

    /// <summary>Los días de vigencia si el parámetro no se puede leer (el defecto sembrado, data-model §10).</summary>
    public const int DiasDeVigenciaPorDefecto = 5;

    public async Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingNumberReservationAttempted, EntidadDeAuditoria, solicitud.IdempotencyKey,
            new { method = "ReservarAsync", payment = solicitud.PaymentPublicId, document = solicitud.DocumentPublicId, person = solicitud.PersonPublicId,
                amount = solicitud.Amount, line = solicitud.CreditLineCode }, ct);
        var validacion = await validadorDeReserva.ValidateAsync(solicitud, ct);
        if (!validacion.IsValid) return Rechazo(ErroresDePagos.CodigoValidacion, validacion.Errors[0].ErrorMessage);
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return ReservasDeCarteraNoHabilitada.NoHabilitada;
        if (!transaccion.HayEnCurso())
            return Rechazo(ErroresDePagos.CodigoTransaccionRequerida, "La reserva debe pedirse dentro de la transacción de la venta.");

        return await PuntoDeGuardado.EjecutarAsync(db, PuntoDeLaReserva, () => ReservarBajoCerrojoAsync(solicitud, ct),
            e => new ResultadoDeReservaDto(EstadosDeReserva.NoResponse, null, null, null, EstadosDeReserva.NoResponse, e.Message), ct);
    }

    public async Task<ResultadoDeReservaDto> LiberarAsync(SolicitudDeLiberacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingNumberReservationAttempted, EntidadDeAuditoria, solicitud.IdempotencyKey,
            new { method = "LiberarAsync", payment = solicitud.PaymentPublicId, reason = solicitud.Reason }, ct);
        var validacion = await validadorDeLiberacion.ValidateAsync(solicitud, ct);
        if (!validacion.IsValid) return Rechazo(ErroresDePagos.CodigoValidacion, validacion.Errors[0].ErrorMessage);
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return ReservasDeCarteraNoHabilitada.NoHabilitada;
        if (!transaccion.HayEnCurso())
            return Rechazo(ErroresDePagos.CodigoTransaccionRequerida, "La liberación debe pedirse dentro de la transacción de quien descarta la venta.");

        return await PuntoDeGuardado.EjecutarAsync(db, PuntoDeLaReserva, () => LiberarBajoCerrojoAsync(solicitud, ct),
            e => new ResultadoDeReservaDto(EstadosDeReserva.NoResponse, null, null, null, EstadosDeReserva.NoResponse, e.Message), ct);
    }

    private async Task<ResultadoDeReservaDto> ReservarBajoCerrojoAsync(SolicitudDeReserva s, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == s.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is not { } personId) return Rechazo(ErroresDeVentasACredito.CodigoPersonaInexistente, ErroresDeVentasACredito.PersonaInexistente().Message);
        var huella = Huella(s);

        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([personId], ModoDeEspera.Esperar, ct);

        // Idempotencia bajo el cerrojo: la misma clave y la misma huella devuelven lo mismo.
        var existente = await db.LoanNumberReservations.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == s.IdempotencyKey && !r.IsDeleted, ct)
                        ?? db.LoanNumberReservations.Local.FirstOrDefault(r => r.IdempotencyKey == s.IdempotencyKey);
        if (existente is not null)
            return existente.RequestSha256 == huella
                ? Respuesta(existente, null, null)
                : Rechazo(ErroresDeVentasACredito.CodigoReservaClaveReusada, ErroresDeVentasACredito.ReservaClaveReusada().Message);

        var fecha = s.OperationDate;
        var vigentes = await bloqueos.VigentesAsync([personId], ct);
        if (vigentes.Count > 0)
            return Rechazo(ErroresDeVentasACredito.CodigoReservaBloqueada, ConsultasDeCartera.MotivoDe(vigentes.OrderBy(b => (int)b.Reason).First().Reason));
        var dias = await DiasDeMoraAsync(personId, fecha, ct);
        if (dias > 0 && dias > await EnteroAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, fecha, 0, ct))
            return Rechazo(ErroresDeVentasACredito.CodigoReservaBloqueada, $"Tiene {dias} días de mora: más de los que admite la venta a crédito.");

        var linea = string.IsNullOrWhiteSpace(s.CreditLineCode)
            ? null
            : await db.CreditLines.AsNoTracking().Where(l => l.Code == s.CreditLineCode.Trim().ToUpperInvariant() && !l.IsDeleted && l.Nature == LineNature.Placement)
                .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        var cupo = await cupos.ParaVentaAsync(personId, linea, fecha, ct, sinElPago: s.PaymentPublicId);
        if (cupo is not null && s.Amount > cupo.Disponible)
            return Rechazo(ErroresDeVentasACredito.CodigoReservaSinCupo, ErroresDeVentasACredito.MensajeSinCupo(cupo.Disponible));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario)
            return Rechazo(Segregation.RegistroDeIntervenciones.CodigoSinPersona, "La reserva la pide el usuario de Comercial que confirma la venta.");
        var hoy = reloj.HoyLocal;
        // El número, al final del orden de cerrojos (C24).
        var numero = await numerador.AsignarAsync(LendingNumberKind.Loan, string.Empty, TramoDeDesembolso.PrefijoDeCredito, ct);
        var reserva = new LoanNumberReservation
        {
            Number = numero,
            PersonId = personId,
            Amount = s.Amount,
            CreditLineId = linea,
            CreditLimitId = cupo?.Cupo.Id is > 0 ? cupo.Cupo.Id : null,
            IdempotencyKey = s.IdempotencyKey,
            RequestSha256 = huella,
            SalesDocumentPublicId = s.DocumentPublicId,
            PaymentPublicId = s.PaymentPublicId,
            ConsultationEvidence = s.ConsultationEvidence,
            Status = LoanNumberReservationStatus.Reserved,
            ReservedAt = reloj.UtcNow,
            ReservedByUserId = usuario,
            ExpiresOn = hoy.AddDays(await EnteroAsync(ParametrosDeCartera.ReservasDiasDeVigencia, hoy, DiasDeVigenciaPorDefecto, ct)),
        };
        db.LoanNumberReservations.Add(reserva);
        await auditoria.HechoAsync(AuditEventTypes.LendingNumberReservationReserved, EntidadDeAuditoria, s.IdempotencyKey,
            new { reservation = reserva.PublicId, number = numero, amount = s.Amount, payment = s.PaymentPublicId, expiresOn = reserva.ExpiresOn,
                evidence = s.ConsultationEvidence }, ct);
        return Respuesta(reserva, null, null);
    }

    private async Task<ResultadoDeReservaDto> LiberarBajoCerrojoAsync(SolicitudDeLiberacion s, CancellationToken ct)
    {
        var personaId = await db.LoanNumberReservations.AsNoTracking().Where(r => r.IdempotencyKey == s.IdempotencyKey && !r.IsDeleted)
            .Select(r => (int?)r.PersonId).FirstOrDefaultAsync(ct);
        if (personaId is not { } persona) return Rechazo(ErroresDeVentasACredito.CodigoReservaInexistente, ErroresDeVentasACredito.ReservaInexistente().Message);
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([persona], ModoDeEspera.Esperar, ct);

        var reserva = await db.LoanNumberReservations.FirstAsync(r => r.IdempotencyKey == s.IdempotencyKey && !r.IsDeleted, ct);
        switch (reserva.Status)
        {
            case LoanNumberReservationStatus.Reserved:
                var actor = await actorActual.ObtenerAsync(ct);
                reserva.Status = LoanNumberReservationStatus.Released;
                reserva.ReleasedAt = reloj.UtcNow;
                reserva.ReleasedByUserId = actor.UserId;
                reserva.ReleaseReason = s.Reason.Trim();
                await auditoria.HechoAsync(AuditEventTypes.LendingNumberReservationReleased, EntidadDeAuditoria, s.IdempotencyKey,
                    new { reservation = reserva.PublicId, number = reserva.Number, reason = reserva.ReleaseReason }, ct);
                return Respuesta(reserva, null, null);
            case LoanNumberReservationStatus.Used:
                // Una usada responde su estado sin cambiarlo, con el código que la nombra (notas de coordinación PE, contradicción 7).
                return Respuesta(reserva, ErroresDeVentasACredito.CodigoReservaUsada, ErroresDeVentasACredito.MensajeReservaUsada);
            default:
                return Respuesta(reserva, null, null);
        }
    }

    /// <summary>La huella de la petición (sin la evidencia, que puede venir de otra consulta del mismo pago).</summary>
    public static string Huella(SolicitudDeReserva s) => HuellaDeOperacion.Calcular(MetodoReservar, new
    {
        key = s.IdempotencyKey, payment = s.PaymentPublicId, document = s.DocumentPublicId, person = s.PersonPublicId, meansClass = s.MeansClass.ToString(),
        amount = s.Amount, operationDate = s.OperationDate, line = s.CreditLineCode?.Trim().ToUpperInvariant() ?? string.Empty, channel = s.SalesChannelCode,
    });

    private async Task<int> DiasDeMoraAsync(int personId, DateOnly fecha, CancellationToken ct)
    {
        var creditos = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == personId && !l.IsDeleted && l.Status == LoanStatus.Active)
            .Select(l => l.Id).ToListAsync(ct);
        if (creditos.Count == 0) return 0;
        var posiciones = await db.LoanDailyPositions.AsNoTracking().Where(p => creditos.Contains(p.LoanId) && p.PositionDate <= fecha)
            .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.DaysPastDue }).ToListAsync(ct);
        return posiciones.GroupBy(p => p.LoanId)
            .Select(g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First().DaysPastDue)
            .DefaultIfEmpty(0).Max();
    }

    private async Task<int> EnteroAsync(string clave, DateOnly fecha, int porDefecto, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess && leido.Value.Valor is int d && d >= 0 ? d : porDefecto;
    }

    private static ResultadoDeReservaDto Respuesta(LoanNumberReservation r, string? codigo, string? mensaje) =>
        new(r.Status.ToString(), r.Number, r.PublicId, r.ExpiresOn, codigo, mensaje);

    private static ResultadoDeReservaDto Rechazo(string codigo, string mensaje) => new(EstadosDeReserva.Rejected, null, null, null, codigo, mensaje);
}
