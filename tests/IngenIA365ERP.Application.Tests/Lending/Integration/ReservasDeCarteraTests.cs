using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Integration.Reservas;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0568 (FR-057, FR-058, T21, T22, T56, T68, C24, C25, C31, D26; interfaces.md §5.2; data-model §2.11): <see cref="ReservasDeCartera"/>,
/// la implementación real de <c>IReservasDeCartera</c>: validador explícito; la reserva con el número del numerador, su vencimiento por
/// <c>Reservas.DiasDeVigencia</c> y la evidencia de la consulta; la misma clave y huella dan el mismo número y otra huella es
/// <c>Lending.Reservation.KeyReused</c>; sin cupo o bloqueado, <c>Rejected</c>; liberar (repetido, usada, vencida, inexistente); el intento aparte y
/// el hecho en el outbox; sin consultar la matriz contable; una falla técnica es <c>NoResponse</c>; antes de activar, <c>NotEnabled</c>; y nunca guarda
/// por su cuenta.
/// </summary>
public sealed class ReservasDeCarteraTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        _v.G.ActuarComo(_v.Cajera);
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private SolicitudDeReserva Solicitud(decimal monto = 400_000m, Guid? pago = null, string? clave = null)
    {
        var p = pago ?? Guid.NewGuid();
        return new SolicitudDeReserva(clave ?? SolicitudDeReservaValidator.ClaveDeReserva(p, monto), p, Guid.NewGuid(), _v.Ana.PublicId,
            PaymentMeansClass.AssociateCredit, monto, VentasACreditoDePrueba.Hoy, "CONLIB", "MOSTRADOR")
        {
            ConsultationEvidence = "evidencia-1",
        };
    }

    private async Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva s, ReservasDeCartera? reservas = null)
    {
        var r = await (reservas ?? _v.Reservas()).ReservarAsync(s, default);
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<ResultadoDeReservaDto> LiberarAsync(SolicitudDeLiberacion s)
    {
        var r = await _v.Reservas().LiberarAsync(s, default);
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Reserva_un_numero_con_su_vencimiento_y_la_evidencia_de_la_consulta()
    {
        var s = Solicitud();

        var r = await ReservarAsync(s);

        r.Status.Should().Be(EstadosDeReserva.Reserved);
        r.LoanNumber.Should().StartWith("CR-");
        r.ExpiresOn.Should().Be(VentasACreditoDePrueba.Hoy.AddDays(5), "Reservas.DiasDeVigencia por defecto");
        var fila = await _v.Db.LoanNumberReservations.AsNoTracking().SingleAsync();
        (fila.Number, fila.PaymentPublicId, fila.Amount, fila.ConsultationEvidence, fila.ReservedByUserId, fila.IdempotencyKey).Should()
            .Be((r.LoanNumber, s.PaymentPublicId, 400_000m, "evidencia-1", _v.Cajera.Id, s.IdempotencyKey));
        fila.PublicId.Should().Be(r.ReservationPublicId!.Value);
    }

    [Fact]
    public async Task La_misma_clave_y_huella_dan_el_mismo_numero_y_otra_huella_es_KeyReused()
    {
        var s = Solicitud();
        var primera = await ReservarAsync(s);

        var repetida = await ReservarAsync(s);
        var otra = await ReservarAsync(s with { SalesChannelCode = "OTRO" });

        repetida.LoanNumber.Should().Be(primera.LoanNumber);
        (await _v.Db.LoanNumberReservations.CountAsync()).Should().Be(1);
        (otra.Status, otra.Code).Should().Be((EstadosDeReserva.Rejected, ErroresDeVentasACredito.CodigoReservaClaveReusada));
    }

    [Fact]
    public async Task Sin_cupo_suficiente_rechaza_con_QuotaExceeded_y_el_disponible()
    {
        _v.Rotativo(_v.Ana, 300_000m);

        var r = await ReservarAsync(Solicitud(400_000m));

        (r.Status, r.Code).Should().Be((EstadosDeReserva.Rejected, ErroresDeVentasACredito.CodigoReservaSinCupo));
        r.Message.Should().Contain("300.000");
        (await _v.Db.LoanNumberReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_reserva_descuenta_cupo_para_la_siguiente()
    {
        _v.Rotativo(_v.Ana, 700_000m);
        (await ReservarAsync(Solicitud(400_000m))).Status.Should().Be(EstadosDeReserva.Reserved);

        var segunda = await ReservarAsync(Solicitud(400_000m));

        segunda.Code.Should().Be(ErroresDeVentasACredito.CodigoReservaSinCupo, "la primera reserva ya descuenta 400.000 del cupo (FR-057)");
    }

    [Fact]
    public async Task Un_deudor_bloqueado_rechaza_con_Blocked()
    {
        _v.Db.DebtorBlocks.Add(DebtorBlock.Nuevo(_v.Ana.Id, DebtorBlockReason.Manual, "manual:1", VentasACreditoDePrueba.Hoy.AddDays(-1), "Orden de gerencia", null));
        _v.Db.SaveChanges();

        var r = await ReservarAsync(Solicitud());

        (r.Status, r.Code).Should().Be((EstadosDeReserva.Rejected, ErroresDeVentasACredito.CodigoReservaBloqueada));
    }

    [Fact]
    public async Task El_validador_explicito_rechaza_una_clave_que_no_es_la_del_pago_y_el_monto()
    {
        var r = await ReservarAsync(Solicitud(clave: "INV:otra"));

        (r.Status, r.Code).Should().Be((EstadosDeReserva.Rejected, "Validation.Invalid"));
    }

    [Fact]
    public async Task Sin_transaccion_en_curso_no_reserva()
    {
        _v.Transaccion.HayEnCurso().Returns(false);

        var r = await ReservarAsync(Solicitud());

        (r.Status, r.Code).Should().Be((EstadosDeReserva.Rejected, "Lending.Contract.TransactionRequired"));
    }

    [Fact]
    public async Task Una_falla_tecnica_responde_NoResponse_y_no_deja_nada()
    {
        var bloqueos = Substitute.For<Application.Lending.Origination.IBloqueosDelDeudor>();
        bloqueos.VigentesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("base caída"));
        var reservas = new ReservasDeCartera(_v.Db, _v.Transaccion, _v.O.Cerrojo, _v.D.Numerador(), _v.O.Disponible(), bloqueos, _v.D.Parametros(),
            new AuditoriaDeEscriturasDeCartera(_v.Servicios(), _v.Db), new SolicitudDeReservaValidator(), new SolicitudDeLiberacionValidator(), _v.G.Actor,
            _v.G.Reloj);

        var r = await ReservarAsync(Solicitud(), reservas);

        r.Status.Should().Be(EstadosDeReserva.NoResponse, "la venta sigue sin número (D26, C25)");
        (await _v.Db.LoanNumberReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Deja_el_intento_aparte_y_el_hecho_en_el_outbox_sin_consultar_la_matriz_contable()
    {
        await ReservarAsync(Solicitud());

        (await _v.G.EventosAsync()).Should().Equal("Lending.NumberReservation.Attempted", "Lending.NumberReservation.Reserved");
        await _v.D.Contabilidad.DidNotReceiveWithAnyArgs().EvaluarAsync(default!, default);
    }

    [Fact]
    public async Task No_guarda_por_su_cuenta()
    {
        var r = await _v.Reservas().ReservarAsync(Solicitud(), default);

        r.Status.Should().Be(EstadosDeReserva.Reserved);
        _v.Db.ChangeTracker.Clear();
        (await _v.Db.LoanNumberReservations.CountAsync()).Should().Be(0, "confirma Comercial: si su venta se revierte, la reserva nunca existió (T22)");
    }

    // --------------------------------------------------------------------------------------------------------- liberar --

    [Fact]
    public async Task Liberar_una_reservada_la_deja_Released_y_repetir_responde_lo_mismo()
    {
        var s = Solicitud();
        await ReservarAsync(s);
        var liberacion = new SolicitudDeLiberacion(s.IdempotencyKey, s.PaymentPublicId, "Descartado el borrador");

        var r = await LiberarAsync(liberacion);
        var otra = await LiberarAsync(liberacion);

        r.Status.Should().Be(EstadosDeReserva.Released);
        otra.Status.Should().Be(EstadosDeReserva.Released);
        var fila = await _v.Db.LoanNumberReservations.AsNoTracking().SingleAsync();
        (fila.Status, fila.ReleaseReason, fila.ReleasedByUserId).Should().Be((LoanNumberReservationStatus.Released, "Descartado el borrador", (int?)_v.Cajera.Id));
        (await _v.G.EventosAsync()).Should().Contain("Lending.NumberReservation.Released");
    }

    [Theory]
    [InlineData(LoanNumberReservationStatus.Used, EstadosDeReserva.Used, ErroresDeVentasACredito.CodigoReservaUsada)]
    [InlineData(LoanNumberReservationStatus.Expired, EstadosDeReserva.Expired, null)]
    public async Task Liberar_una_usada_o_vencida_responde_su_estado_sin_cambiarlo(LoanNumberReservationStatus estado, string esperado, string? codigo)
    {
        var s = Solicitud();
        await ReservarAsync(s);
        var fila = await _v.Db.LoanNumberReservations.SingleAsync();
        fila.Status = estado;
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();

        var r = await LiberarAsync(new SolicitudDeLiberacion(s.IdempotencyKey, s.PaymentPublicId, "Descartado"));

        (r.Status, r.Code).Should().Be((esperado, codigo));
        (await _v.Db.LoanNumberReservations.AsNoTracking().SingleAsync()).Status.Should().Be(estado);
    }

    [Fact]
    public async Task Liberar_una_inexistente_es_NotFound()
    {
        var r = await LiberarAsync(new SolicitudDeLiberacion("INV:nada:1.00", Guid.NewGuid(), "Descartado"));

        (r.Status, r.Code).Should().Be((EstadosDeReserva.Rejected, ErroresDeVentasACredito.CodigoReservaInexistente));
    }

    [Fact]
    public async Task Antes_de_activar_responde_NotEnabled_sin_escribir()
    {
        var setup = await _v.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        await _v.Db.SaveChangesAsync();

        var r = await ReservarAsync(Solicitud());

        r.Status.Should().Be(EstadosDeReserva.NotEnabled);
        (await _v.Db.LoanNumberReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void La_clave_lleva_el_monto_con_dos_decimales_y_punto() =>
        SolicitudDeReservaValidator.ClaveDeReserva(Guid.Parse("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4b11"), 150000m)
            .Should().Be("INV:e1f2a3b4c5d64e7f8a9b0c1d2e3f4b11:150000.00");
}
