using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 013, US6 (T0576; quickstart §1.5; mensajes.md §11.1, §11.2; data-model parte 3 §8.3): Comercial como destino <c>Inventory</c>.
/// <see cref="DestinoInventario"/> acepta <c>ObligacionCreada</c> v1 y <c>ObligacionRechazada</c> v1 y nada más; su comando escribe
/// <c>INV_LendingObligationResponses</c> (único por <c>MessagePublicId</c>) con número, estado, código, motivo y <c>Retryable</c>, nunca saldos ni cuotas;
/// responde <c>Processed</c> con el <c>PublicId</c> de la fila y <c>AlreadyProcessed</c> al repetir; la pestaña «Crédito» de la venta muestra el número
/// reservado y la última respuesta.
/// </summary>
public class DestinoInventarioTests
{
    private static readonly DateTime Ahora = new(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>Una venta de 120.000 a crédito validada por Cartera, con el número CR-0002001 reservado.</summary>
    private static async Task<(CreditoDePrueba C, Guid Venta, DocumentPayment Pago)> VentaValidadaAsync()
    {
        var c = await CreditoDePrueba.CrearAsync();
        c.Cartera = new ConsultasDeCarteraDePrueba { Respuesta = ConsultasDeCarteraDePrueba.Activa(evidencia: "evt-1") };
        c.Reservas.RespuestaDeReserva = ReservasDeCarteraDePrueba.Reservada("CR-0002001");
        var venta = await c.VentaAsync(c.AsociadoX, total => [c.Credito(c.CredAsoc, total)], c.V.Linea(c.V.P1, 60m));
        (await c.ConfirmarAsync(venta)).IsSuccess.Should().BeTrue();
        c.Db.ChangeTracker.Clear();
        var id = c.V.Documento(venta).Id;
        return (c, venta, c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == id && !p.IsDeleted));
    }

    /// <summary>Un mensaje de Cartera a Comercial con su entrega <c>Inventory</c>, como lo deja el emisor.</summary>
    private static IntegrationMessage Mensaje(CreditoDePrueba c, string tipo, object contenido, Guid venta)
    {
        var json = JsonSerializer.Serialize(contenido, contenido.GetType(), OpcionesDeMensajes.Opciones);
        var mensaje = new IntegrationMessage
        {
            Type = tipo,
            Version = 1,
            Kind = IntegrationMessageKind.Business,
            OriginModule = ModulosDeOrigen.Cartera,
            OriginKind = MessageOriginKind.Document,
            OriginPublicId = Guid.NewGuid(),
            OriginDocumentClass = "InboundMessage",
            OriginDocumentTypeCode = "LIM",
            OriginNumber = "LIM-1",
            OriginEventKey = "Confirmation",
            ChainRootPublicId = venta,
            OperationDate = new DateOnly(2026, 7, 15),
            Currency = "COP",
            ExchangeRate = 1m,
            PayloadJson = json,
            PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            OriginUserCentralId = Guid.NewGuid(),
            OriginUserName = "Cajera",
            EmittedAt = Ahora,
        };
        c.Db.IntegrationMessages.Add(mensaje);
        c.Db.SaveChanges();
        c.Db.IntegrationMessageDeliveries.Add(new IntegrationMessageDelivery
        {
            MessageId = mensaje.Id, Destination = IntegrationDestinations.Inventory, Mode = DeliveryMode.Online, Status = DeliveryStatus.Pending,
        });
        c.Db.SaveChanges();
        return mensaje;
    }

    private static ObligacionCreadaV1 Creada(Guid pago, string estado = "Validated", SaleValidationV1? validacion = null) => new()
    {
        SaleMessage = new SaleMessageRefV1 { MessageId = Guid.NewGuid(), Type = VentaACreditoRegistradaV2.Type, Version = 2 },
        CreditPaymentPublicId = pago,
        Loan = new LoanRefV1 { LoanPublicId = Guid.NewGuid(), LoanNumber = "CR-0002001" },
        CreditLineCode = "CONSUMO",
        Amount = 120_000m,
        OriginDate = new DateOnly(2026, 7, 15),
        Status = estado,
        Validation = validacion,
        Plan = new LoanPlanSummaryV1(),
        NumberSource = "Reservation",
    };

    private static ConsumeLendingObligationResponseCommandHandler Handler(CreditoDePrueba c)
    {
        var reloj = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);
        return new ConsumeLendingObligationResponseCommandHandler(c.Db, new MensajesEntrantes(c.Db), reloj);
    }

    private static async Task<ResultadoDeConsumo> ConsumirAsync(CreditoDePrueba c, IntegrationMessage mensaje)
    {
        c.Db.ChangeTracker.Clear();
        var r = await Handler(c).Handle(new ConsumeLendingObligationResponseCommand([mensaje.PublicId]), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private static GetSalesDocumentCreditQueryHandler Credito(CreditoDePrueba c)
    {
        var registro = Substitute.For<IRegistroDeIntegracion>();
        registro.DestinoHabilitadoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        return new GetSalesDocumentCreditQueryHandler(c.Db, c.V.K.Vista(), c.V.K.Alcance, c.Cartera, registro, c.V.Compras.C.Reloj);
    }

    [Theory]
    [InlineData("ObligacionCreada", 1, true)]
    [InlineData("ObligacionRechazada", 1, true)]
    [InlineData("ObligacionCreada", 2, false)]
    [InlineData("VentaACreditoRegistrada", 2, false)]
    [InlineData("CreditoDesembolsado", 1, false)]
    public void Acepta_solo_las_dos_respuestas_de_Cartera_en_su_version_1(string tipo, int version, bool acepta)
    {
        var destino = new DestinoInventario(Substitute.For<ISender>(), NullLogger<DestinoInventario>.Instance);

        destino.Destino.Should().Be(IntegrationDestinations.Inventory);
        destino.Acepta(tipo, version).Should().Be(acepta);
    }

    [Fact]
    public async Task ObligacionCreada_guarda_numero_y_estado_y_responde_Processed_con_la_fila()
    {
        var (c, venta, pago) = await VentaValidadaAsync();
        var contenido = Creada(pago.PublicId);
        var mensaje = Mensaje(c, ObligacionCreadaV1.Type, contenido, venta);

        var r = await ConsumirAsync(c, mensaje);

        var fila = c.Db.LendingObligationResponses.AsNoTracking().Should().ContainSingle().Subject;
        r.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(fila.PublicId, null, null, MotivoSinComprobante.Informational));
        fila.DocumentPaymentId.Should().Be(pago.Id);
        fila.MessagePublicId.Should().Be(mensaje.PublicId);
        fila.MessageType.Should().Be(ObligacionCreadaV1.Type);
        fila.OriginalMessagePublicId.Should().Be(contenido.SaleMessage.MessageId);
        fila.Outcome.Should().Be(LendingObligationOutcome.Created);
        fila.ObligationPublicId.Should().Be(contenido.Loan.LoanPublicId);
        fila.ObligationNumber.Should().Be("CR-0002001");
        fila.ValidationStatus.Should().Be(LendingObligationState.Validated);
        fila.Retryable.Should().BeFalse();
        typeof(LendingObligationResponse).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Balance") || n.Contains("Installment") || n.Contains("Amount"), "Comercial nunca guarda saldos ni cuotas (T18)");
    }

    [Fact]
    public async Task La_misma_respuesta_dos_veces_es_AlreadyProcessed_con_la_misma_fila()
    {
        var (c, venta, pago) = await VentaValidadaAsync();
        var mensaje = Mensaje(c, ObligacionCreadaV1.Type, Creada(pago.PublicId), venta);

        var primera = await ConsumirAsync(c, mensaje);
        var segunda = await ConsumirAsync(c, mensaje);

        var fila = c.Db.LendingObligationResponses.AsNoTracking().Should().ContainSingle().Subject;
        primera.Should().BeOfType<ResultadoDeConsumo.Processed>();
        segunda.Should().BeEquivalentTo(new ResultadoDeConsumo.AlreadyProcessed(fila.PublicId, SinComprobante: MotivoSinComprobante.Informational));
    }

    [Fact]
    public async Task Una_validacion_fallida_guarda_el_codigo_y_el_motivo()
    {
        var (c, venta, pago) = await VentaValidadaAsync();
        var mensaje = Mensaje(c, ObligacionCreadaV1.Type,
            Creada(pago.PublicId, "ValidationFailed", new SaleValidationV1 { Code = "Lending.Sale.QuotaExceeded", Reason = "Supera el cupo disponible." }), venta);

        await ConsumirAsync(c, mensaje);

        var fila = c.Db.LendingObligationResponses.AsNoTracking().Single();
        fila.ValidationStatus.Should().Be(LendingObligationState.ValidationFailed);
        fila.ReasonCode.Should().Be("Lending.Sale.QuotaExceeded");
        fila.Reason.Should().Be("Supera el cupo disponible.");
    }

    [Theory]
    [InlineData("NoConvertible", null)]
    [InlineData("AceptadaSinMigrar", LendingObligationState.NotIncorporated)]
    [InlineData("SinObligacion", LendingObligationState.NotIncorporated)]
    public async Task ObligacionRechazada_guarda_el_motivo_sin_obligacion(string motivo, LendingObligationState? estado)
    {
        var (c, venta, pago) = await VentaValidadaAsync();
        var mensaje = Mensaje(c, ObligacionRechazadaV1.Type, new ObligacionRechazadaV1
        {
            SaleMessage = new SaleMessageRefV1 { MessageId = Guid.NewGuid(), Type = VentaACreditoRegistradaV2.Type, Version = 2 },
            CreditPaymentPublicId = pago.PublicId,
            EvaluationPublicId = Guid.NewGuid(),
            Motive = motivo,
            Code = "Lending.Sale.PersonNotFound",
            Reason = "La persona no existe en Cartera.",
            Retryable = false,
        }, venta);

        var r = await ConsumirAsync(c, mensaje);

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        var fila = c.Db.LendingObligationResponses.AsNoTracking().Single();
        fila.Outcome.Should().Be(LendingObligationOutcome.Rejected);
        fila.ObligationNumber.Should().BeNull();
        fila.ObligationPublicId.Should().BeNull();
        fila.ValidationStatus.Should().Be(estado);
        fila.ReasonCode.Should().Be(motivo);
        fila.Reason.Should().Be("La persona no existe en Cartera.");
        fila.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task Una_respuesta_por_un_pago_que_Comercial_no_tiene_es_Rejected_sin_fila()
    {
        var (c, venta, _) = await VentaValidadaAsync();
        var mensaje = Mensaje(c, ObligacionCreadaV1.Type, Creada(Guid.NewGuid()), venta);

        var r = await ConsumirAsync(c, mensaje);

        r.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ConsumeLendingObligationResponseCommandHandler.CodigoPagoInexistente);
        c.Db.LendingObligationResponses.Should().BeEmpty();
    }

    [Fact]
    public async Task La_pestana_credito_muestra_el_numero_reservado_y_la_ultima_respuesta()
    {
        var (c, venta, pago) = await VentaValidadaAsync();
        var antes = await Credito(c).Handle(new GetSalesDocumentCreditQuery(venta), default);
        antes.Value.Payments.Single().ReservedLoanNumber.Should().Be("CR-0002001");
        antes.Value.Payments.Single().LendingObligation.Should().BeNull("mientras Cartera no responde no hay respuesta guardada");

        await ConsumirAsync(c, Mensaje(c, ObligacionCreadaV1.Type,
            Creada(pago.PublicId, "ValidationFailed", new SaleValidationV1 { Code = "Lending.Sale.Blocked", Reason = "Deudor bloqueado." }), venta));

        var r = await Credito(c).Handle(new GetSalesDocumentCreditQuery(venta), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var respuesta = r.Value.Payments.Single().LendingObligation!;
        respuesta.Outcome.Should().Be("Created");
        respuesta.ObligationNumber.Should().Be("CR-0002001");
        respuesta.ValidationStatus.Should().Be("ValidationFailed");
        respuesta.Reason.Should().Be("Deudor bloqueado.");
    }
}
