using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 013, T0116 (T95; contracts/interfaces.md §5.3; R111, R112, R115): lo que <see cref="CreditoEnLaVenta"/> hace con cada respuesta
/// de Cartera al confirmar una venta a crédito, con el doble de consultas (<see cref="ConsultasDeCarteraDePrueba"/>) y el de reservas
/// (<see cref="ReservasDeCarteraDePrueba"/>):
/// <list type="bullet">
/// <item><c>NotEnabled</c> → crédito provisional como en la 012 (aprobación, «pendiente de validar»), sello «Contabilidad», sin reserva;</item>
/// <item><c>Blocked</c> o <c>AllowsSale = false</c> → <c>Inventory.Credit.LendingBlocked</c>; más que el cupo →
/// <c>Inventory.Credit.QuotaExceeded</c> con <c>availableQuota</c>;</item>
/// <item>sin respuesta → <c>Cartera.PoliticaSinRespuesta</c> (<c>Bloquear</c> → <c>Inventory.Credit.LendingNoResponse</c>;
/// <c>PermitirConAprobacion</c> → origen <c>LendingNoResponse</c> con aprobación);</item>
/// <item>válida → origen <c>Validated</c>, sin aprobación provisional, <c>ConsultationEvidence</c> = <c>EvidenceId</c>; la reserva
/// <c>Reserved</c> deja número y reserva en el pago (clave <c>INV:{pago:N}:{monto}</c>), <c>Rejected</c> detiene la confirmación y
/// <c>NoResponse</c> la deja sin número (D26);</item>
/// <item>un pago con <c>CreditWithoutLending</c> no consulta, no reserva, sella «Contabilidad» y no emite a Cartera (A6).</item>
/// </list>
/// </summary>
public class CreditoEnLaVentaTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "OK";

    private static async Task<(CreditoDePrueba C, ConsultasDeCarteraDePrueba Cartera)> CrearAsync(EstadoCrediticioDto? respuesta = null)
    {
        var c = await CreditoDePrueba.CrearAsync();
        var cartera = new ConsultasDeCarteraDePrueba { Respuesta = respuesta ?? ConsultasDeCarteraDePrueba.Activa() };
        c.Cartera = cartera;
        return (c, cartera);
    }

    /// <summary>Una venta de 120.000 (60 × P1 a 2.000) al asociado X, toda a crédito con CREDASOC.</summary>
    private static Task<Guid> VentaAsync(CreditoDePrueba c) =>
        c.VentaAsync(c.AsociadoX, total => [c.Credito(c.CredAsoc, total)], c.V.Linea(c.V.P1, 60m));

    private static DocumentPayment Pago(CreditoDePrueba c, Guid venta)
    {
        c.Db.ChangeTracker.Clear();
        var id = c.V.Documento(venta).Id;
        return c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == id && !p.IsDeleted);
    }

    [Fact]
    public void La_clave_de_la_reserva_lleva_el_pago_y_el_monto_con_dos_decimales_y_punto()
    {
        var pago = Guid.Parse("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4b11");

        CreditoEnLaVenta.ClaveDeReserva(pago, 150_000m).Should().Be("INV:e1f2a3b4c5d64e7f8a9b0c1d2e3f4b11:150000.00");
        CreditoEnLaVenta.ClaveDeReserva(pago, 1234.5m).Should().Be("INV:e1f2a3b4c5d64e7f8a9b0c1d2e3f4b11:1234.50");
    }

    [Fact]
    public async Task Con_Cartera_no_habilitada_rige_el_credito_provisional_y_no_se_reserva()
    {
        var (c, cartera) = await CrearAsync(CarteraNoHabilitada.Instancia);
        c.V.Parametro(ParametrosDeInventario.CarteraIntegracionHabilitadaDesde, "2026-01-01");
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        r.Value.Status.Should().Be(DocumentStatus.PendingApproval, "el provisional pide la aprobación por monto");
        var pago = Pago(c, venta);
        pago.CreditOrigin.Should().Be(CreditOrigin.ProvisionalCredit);
        pago.PendingValidation.Should().BeTrue();
        pago.AccountsReceivableRecordedBy.Should().Be(CreditoEnLaVenta.Contabilidad, "sin Cartera habilitada el sello es Contabilidad");
        pago.ReservedLoanNumber.Should().BeNull();
        cartera.Consultas.Should().ContainSingle();
        c.Reservas.Reservas.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EstadosCrediticios.Blocked, true)]
    [InlineData(EstadosCrediticios.InArrears, false)]
    [InlineData(EstadosCrediticios.NoRelationship, false)]
    public async Task Bloqueada_o_sin_admitir_la_venta_es_LendingBlocked(string estado, bool admite)
    {
        var respuesta = ConsultasDeCarteraDePrueba.Activa() with { Status = estado };
        var (c, _) = await CrearAsync(respuesta with { AllowsSale = admite, BlockReason = "bloqueo manual" });
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be(ErroresDeCredito.LendingBlockedCode);
        c.V.Documento(venta).Status.Should().Be(DocumentStatus.Draft);
        c.Reservas.Reservas.Should().BeEmpty();
    }

    [Fact]
    public async Task Mas_que_el_cupo_disponible_es_QuotaExceeded_con_el_cupo()
    {
        var (c, _) = await CrearAsync(ConsultasDeCarteraDePrueba.Activa(cupo: 100_000m));
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be(ErroresDeCredito.QuotaExceededCode);
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { availableQuota = (decimal?)100_000m }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Sin_respuesta_con_la_politica_por_defecto_Bloquear_es_LendingNoResponse()
    {
        var (c, cartera) = await CrearAsync();
        cartera.Caida = new TimeoutException("Cartera no respondió");
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be(ErroresDeCredito.LendingNoResponseCode);
    }

    [Fact]
    public async Task Sin_respuesta_con_PermitirConAprobacion_queda_LendingNoResponse_con_aprobacion()
    {
        var (c, cartera) = await CrearAsync();
        cartera.Caida = new TimeoutException("Cartera no respondió");
        c.V.K.Parametro(ParametrosDeInventario.CarteraPoliticaSinRespuesta, CreditoEnLaVenta.PermitirConAprobacion,
            Domain.Enums.Parameters.ParameterScopeKind.ThirdPartyKind, (int)PaymentMeansClass.AssociateCredit);
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        var pago = Pago(c, venta);
        pago.CreditOrigin.Should().Be(CreditOrigin.LendingNoResponse);
        pago.PendingValidation.Should().BeTrue();
        c.Reservas.Reservas.Should().BeEmpty("sin respuesta la venta sigue sin número");
    }

    [Fact]
    public async Task La_consulta_valida_confirma_sin_aprobacion_provisional_con_la_evidencia_y_el_numero_reservado()
    {
        var (c, cartera) = await CrearAsync(ConsultasDeCarteraDePrueba.Activa(evidencia: "evt-cupo-1"));
        var reserva = ReservasDeCarteraDePrueba.Reservada("CR-0002001");
        c.Reservas.RespuestaDeReserva = reserva;
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed, "un crédito validado por Cartera no lleva aprobación provisional");
        var pago = Pago(c, venta);
        pago.CreditOrigin.Should().Be(CreditOrigin.Validated);
        pago.PendingValidation.Should().BeFalse();
        pago.ConsultationEvidence.Should().Be("evt-cupo-1");
        pago.CreditLineCode.Should().Be("CONSUMO");
        pago.ReservedLoanNumber.Should().Be("CR-0002001");
        pago.ReservationPublicId.Should().Be(reserva.ReservationPublicId);
        pago.ApprovalRequestId.Should().BeNull();

        var pedida = c.Reservas.Reservas.Should().ContainSingle().Subject;
        pedida.IdempotencyKey.Should().Be(CreditoEnLaVenta.ClaveDeReserva(pago.PublicId, 120_000m));
        pedida.Amount.Should().Be(120_000m);
        pedida.PersonPublicId.Should().Be(c.AsociadoX.PublicId);
        pedida.CreditLineCode.Should().Be("CONSUMO");
        pedida.ConsultationEvidence.Should().Be("evt-cupo-1", "T0599, C31: la evidencia de la consulta viaja con la reserva");
        cartera.Consultas.Should().NotBeEmpty();
    }

    [Fact]
    public async Task La_reserva_repetida_que_Cartera_responde_Used_deja_el_mismo_numero()
    {
        // T0575 (US6-2): la misma clave y huella responde el mismo número aunque la reserva ya se haya usado.
        var (c, _) = await CrearAsync();
        var reserva = ReservasDeCarteraDePrueba.Reservada("CR-0002003") with { Status = EstadosDeReserva.Used };
        c.Reservas.RespuestaDeReserva = reserva;
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        var pago = Pago(c, venta);
        pago.ReservedLoanNumber.Should().Be("CR-0002003");
        pago.ReservationPublicId.Should().Be(reserva.ReservationPublicId);
    }

    [Fact]
    public async Task La_reserva_que_responde_NotEnabled_deja_la_venta_validada_sin_numero()
    {
        // T0575: Cartera se desactivó entre la consulta y la reserva: la venta sigue sin número, como D26.
        var (c, _) = await CrearAsync();
        c.Reservas.RespuestaDeReserva = ReservasDeCarteraNoHabilitada.NoHabilitada;
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        Pago(c, venta).ReservedLoanNumber.Should().BeNull();
    }

    [Theory]
    [InlineData(CreditoEnLaVenta.ReservaSinCupo, ErroresDeCredito.QuotaExceededCode)]
    [InlineData("Lending.Reservation.Blocked", ErroresDeCredito.LendingBlockedCode)]
    public async Task La_reserva_rechazada_detiene_la_confirmacion_con_el_codigo_de_Comercial(string deCartera, string esperado)
    {
        var (c, _) = await CrearAsync();
        c.Reservas.RespuestaDeReserva = new ResultadoDeReservaDto(EstadosDeReserva.Rejected, null, null, null, deCartera, "Cartera la rechazó");
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        Codigo(r).Should().Be(esperado);
        c.V.Documento(venta).Number.Should().BeNull();
    }

    [Fact]
    public async Task La_reserva_sin_respuesta_deja_la_venta_confirmada_sin_numero()
    {
        var (c, _) = await CrearAsync();
        c.Reservas.Caida = new InvalidOperationException("falla técnica de la reserva");
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);

        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        var pago = Pago(c, venta);
        pago.ReservedLoanNumber.Should().BeNull("D26: el número llega con ObligacionCreada");
        pago.ReservationPublicId.Should().BeNull();
        pago.CreditOrigin.Should().Be(CreditOrigin.Validated);
    }

    [Fact]
    public async Task Un_pago_CreditWithoutLending_no_consulta_no_reserva_sella_Contabilidad_y_no_emite_a_Cartera()
    {
        var (c, cartera) = await CrearAsync();
        c.V.Parametro(ParametrosDeInventario.CarteraIntegracionHabilitadaDesde, "2026-01-01");
        var medio = c.Db.PaymentMeans.Single(m => m.Id == c.CredAsoc.Id);
        medio.CreditWithoutLending = true;
        await c.Db.SaveChangesAsync();
        var venta = await VentaAsync(c);

        var r = await c.ConfirmarAsync(venta);
        var aprobada = await c.AprobarAsync(c.SolicitudDeCredito(venta), CreditoDePrueba.Supervisor);

        r.Value.Status.Should().Be(DocumentStatus.PendingApproval, "conserva la aprobación ProvisionalCredit (R115)");
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Message : string.Empty);
        cartera.Consultas.Should().BeEmpty();
        c.Reservas.Reservas.Should().BeEmpty();
        var pago = Pago(c, venta);
        pago.CreditWithoutLending.Should().BeTrue();
        pago.AccountsReceivableRecordedBy.Should().Be(CreditoEnLaVenta.Contabilidad);
        c.V.Documento(venta).Status.Should().Be(DocumentStatus.Confirmed);
        c.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == venta && m.Type == VentaACreditoRegistradaV2.Type)
            .Should().BeEmpty("D29: el crédito sin Cartera no se emite a Lending");
    }

    [Fact]
    public async Task Descartar_el_borrador_de_oficina_libera_la_reserva_por_su_clave()
    {
        // Feature 013 (T0117, T22): DiscardInventoryDraftCommand llama LiberarAsync con la clave de la reserva.
        var (c, _) = await CrearAsync();
        var venta = await VentaAsync(c);
        var id = c.V.Documento(venta).Id;
        var pago = c.Db.DocumentPayments.Single(p => p.DocumentId == id && !p.IsDeleted);
        pago.ReservedLoanNumber = "CR-0004001";
        pago.ReservationPublicId = Guid.NewGuid();
        await c.Db.SaveChangesAsync();

        var r = await new IngenIA365ERP.Application.Inventory.Documents.DiscardInventoryDraftCommandHandler(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Vista(), c.Credito())
            .Handle(new IngenIA365ERP.Application.Inventory.Documents.DiscardInventoryDraftCommand(venta, DocumentClassGroup.Sales, "Cliente desistió"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var liberada = c.Reservas.Liberaciones.Should().ContainSingle().Subject;
        liberada.IdempotencyKey.Should().Be(CreditoEnLaVenta.ClaveDeReserva(pago.PublicId, pago.Amount));
        liberada.Reason.Should().Be("Cliente desistió");
    }

    [Fact]
    public async Task La_venta_validada_emite_la_v2_con_el_numero_la_reserva_la_linea_y_la_evidencia()
    {
        var (c, _) = await CrearAsync(ConsultasDeCarteraDePrueba.Activa(evidencia: "evt-cupo-2"));
        var reserva = ReservasDeCarteraDePrueba.Reservada("CR-0002002");
        c.Reservas.RespuestaDeReserva = reserva;
        var venta = await VentaAsync(c);

        (await c.ConfirmarAsync(venta)).IsSuccess.Should().BeTrue();

        var mensaje = c.Db.IntegrationMessages.AsNoTracking().Single(m => m.OriginPublicId == venta && m.Type == VentaACreditoRegistradaV2.Type);
        mensaje.Version.Should().Be(2, "Comercial emite sólo la v2 (T18)");
        var contenido = JsonNode.Parse(mensaje.PayloadJson)!;
        contenido["reservedLoanNumber"]!.GetValue<string>().Should().Be("CR-0002002");
        contenido["reservationPublicId"]!.GetValue<Guid>().Should().Be(reserva.ReservationPublicId!.Value);
        contenido["creditLineCode"]!.GetValue<string>().Should().Be("CONSUMO");
        contenido["consultationEvidence"]!.GetValue<string>().Should().Be("evt-cupo-2");
        contenido["origin"]!.GetValue<string>().Should().Be("Validated");
        contenido["pendingValidation"]!.GetValue<bool>().Should().BeFalse();
    }
}
