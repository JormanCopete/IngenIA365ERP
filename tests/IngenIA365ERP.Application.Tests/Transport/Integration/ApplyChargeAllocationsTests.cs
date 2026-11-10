using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Integration;
using IngenIA365ERP.Application.Transport.Notifications;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Integration;

/// <summary>
/// Feature 020 (T169; R12 a R14): lo que hacen con los cargos de servicios los pagos y anulaciones que escucha Transporte. Un mensaje ajeno no
/// escribe; un pago parcial no avisa; el que salda avisa una sola vez; el mismo mensaje no asigna dos veces; la reversa tras avisar deja el
/// envío «pago anulado»; el repago no avisa de nuevo; la factura anulada anula los cargos abiertos.
/// </summary>
public class ApplyChargeAllocationsTests
{
    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 12, 5);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IConsultaDeObligaciones _cartera = Substitute.For<IConsultaDeObligaciones>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly Guid _obligacion = Guid.NewGuid();
    private readonly Guid _factura = Guid.NewGuid();
    private Person _dueno = null!;
    private ServiceCharge _cargo = null!;

    public ApplyChargeAllocationsTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(Hoy);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Process, null, null, null, "Proceso de integración", null, ExecutionChannel.Process, "Tarea:integracion", null, null));
        _cartera.PorPagoDeVentaAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<ObligacionDeVentaDto>());
    }

    private async Task SembrarAsync(bool conConector)
    {
        _db.Branches.Add(new Branch { Name = "Principal" });
        _dueno = new Person { TaxId = "1234567", FirstName = "Ana", LastName = "Prueba" };
        _db.People.Add(_dueno);
        await _db.SaveChangesAsync();

        Connector? conector = null;
        if (conConector)
        {
            conector = new Connector { Code = "RADIO", Name = "Radio", IsEnabled = true, CurrentVersionNumber = 1 };
            conector.Versions.Add(new ConnectorVersion { Number = 1, BaseUrl = "https://proveedor.example.com", DefinitionJson = "{}", DefinitionSha256 = "x" });
            _db.Connectors.Add(conector);
        }
        var servicio = new TransportService { ProductCode = "RADIO", Name = "Radio", Priority = 1, Connector = conector };
        var vehiculo = Vehicle.Registrar(_dueno, Hoy.AddYears(-1));
        vehiculo.Plate = "ABC123";
        vehiculo.InternalNumber = "0009";
        vehiculo.RadioCode = "123";
        _cargo = new ServiceCharge
        {
            Vehicle = vehiculo, Service = servicio, Period = "202612", OwnerPersonId = _dueno.Id, InvoicePublicId = _factura, InvoiceNumber = "FS-1",
            Amount = 50_000m, Route = ChargeRoute.Lending, LoanPublicId = _obligacion, PaymentPublicId = Guid.NewGuid(),
            BillingRun = new BillingRun { Period = "202612" },
        };
        _db.ServiceCharges.Add(_cargo);
        await _db.SaveChangesAsync();
    }

    private ApplyChargeAllocationsCommandHandler Handler() => new(
        _db, _entrantes,
        new EmisionDeSolicitudDeConector(_db, new EmisorDeMensajes(_db, _actor, _reloj), new FacturasDeServicios(_db), _reloj,
            NullLogger<EmisionDeSolicitudDeConector>.Instance),
        _reloj, _cartera);

    private Guid Recibir(object contenido, string tipo)
    {
        var id = Guid.NewGuid();
        var json = System.Text.Json.JsonSerializer.Serialize(contenido);
        var sobre = new IntegrationEnvelopeV1 { MessageId = id, Type = tipo, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR" };
        var mensaje = new MensajeEntrante(sobre, contenido, json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), null,
            new EntregaEntrante(IntegrationDestinations.Transport, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null));
        _entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.Contains(id)), IntegrationDestinations.Transport, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>([mensaje]));
        return id;
    }

    private PagoAplicadoV1 Pago(decimal capital, bool saldada = false, Guid? obligacion = null) => new()
    {
        Loan = new LoanRefV1 { LoanPublicId = obligacion ?? _obligacion, LoanNumber = "C-1" },
        DebtorPersonPublicId = _dueno.PublicId,
        ValueDate = Hoy,
        Amounts = [new ConceptAmountV1 { Concept = "Principal", Amount = capital }],
        PaidOff = saldada,
        Collector = new CollectorRefV1 { Receipt = new ReceiptRefV1 { Number = "RC-9", Date = Hoy } },
    };

    private PagoReversadoV1 Reversa(decimal capital) => new()
    {
        Loan = new LoanRefV1 { LoanPublicId = _obligacion, LoanNumber = "C-1" },
        DebtorPersonPublicId = _dueno.PublicId,
        ValueDate = Hoy,
        Amounts = [new ConceptAmountV1 { Concept = "Principal", Amount = capital }],
    };

    private async Task<ResultadoDeConsumo> AplicarAsync(Guid mensaje) =>
        (await Handler().Handle(new ApplyChargeAllocationsCommand([mensaje]), CancellationToken.None)).Value;

    [Fact]
    public async Task Un_mensaje_que_no_toca_ningun_cargo_no_escribe()
    {
        await SembrarAsync(conConector: false);
        var id = Recibir(Pago(50_000m, obligacion: Guid.NewGuid()), PagoAplicadoV1.Type);

        var r = await AplicarAsync(id);

        r.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        _db.ChargeAllocations.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_pago_parcial_asigna_y_no_avisa()
    {
        await SembrarAsync(conConector: true);

        await AplicarAsync(Recibir(Pago(30_000m), PagoAplicadoV1.Type));

        _cargo.Status.Should().Be(ChargeStatus.Open);
        _db.ChargeAllocations.Should().ContainSingle().Which.Amount.Should().Be(30_000m);
        _db.ConnectorRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task El_pago_que_salda_avisa_una_sola_vez_con_el_identificador_del_cargo()
    {
        await SembrarAsync(conConector: true);

        await AplicarAsync(Recibir(Pago(50_000m), PagoAplicadoV1.Type));

        _cargo.Status.Should().Be(ChargeStatus.Paid);
        var envio = _db.ConnectorRequests.Should().ContainSingle().Subject;
        envio.ExternalId.Should().Be(_cargo.ExternalId);
        envio.Status.Should().Be(ConnectorRequestStatus.Pending);
        _cargo.NotifiedRequestPublicId.Should().Be(envio.PublicId);
    }

    [Fact]
    public async Task El_mismo_mensaje_dos_veces_no_asigna_dos_veces()
    {
        await SembrarAsync(conConector: false);
        var id = Recibir(Pago(20_000m), PagoAplicadoV1.Type);

        await AplicarAsync(id);
        var segunda = await AplicarAsync(id);

        segunda.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        _db.ChargeAllocations.Should().ContainSingle();
    }

    [Fact]
    public async Task La_reversa_despues_de_avisar_deja_el_envio_con_el_pago_anulado()
    {
        await SembrarAsync(conConector: true);
        await AplicarAsync(Recibir(Pago(50_000m), PagoAplicadoV1.Type));
        _db.ConnectorRequests.Single().Status = ConnectorRequestStatus.Sent;
        await _db.SaveChangesAsync();

        await AplicarAsync(Recibir(Reversa(50_000m), PagoReversadoV1.Type));

        _cargo.Status.Should().Be(ChargeStatus.Open);
        _cargo.ReviewFlag.Should().Be(ChargeReviewFlag.PaymentVoidedAfterNotice);
        _db.ConnectorRequests.Single().Status.Should().Be(ConnectorRequestStatus.PaymentVoided);
    }

    [Fact]
    public async Task El_repago_tras_la_anulacion_no_avisa_de_nuevo()
    {
        await SembrarAsync(conConector: true);
        await AplicarAsync(Recibir(Pago(50_000m), PagoAplicadoV1.Type));
        await AplicarAsync(Recibir(Reversa(50_000m), PagoReversadoV1.Type));

        await AplicarAsync(Recibir(Pago(50_000m), PagoAplicadoV1.Type));

        _cargo.Status.Should().Be(ChargeStatus.Paid);
        _cargo.ReviewFlag.Should().Be(ChargeReviewFlag.PaidAgainAfterVoid);
        _db.ConnectorRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task La_factura_anulada_anula_los_cargos_sin_pago()
    {
        await SembrarAsync(conConector: false);
        var anulacion = new DocumentoAnuladoV1 { VoidedDocument = new DocumentRefV1 { PublicId = _factura, Number = "FS-1" } };

        await AplicarAsync(Recibir(anulacion, DocumentoAnuladoV1.Type));

        _cargo.Status.Should().Be(ChargeStatus.Voided);
    }
}
