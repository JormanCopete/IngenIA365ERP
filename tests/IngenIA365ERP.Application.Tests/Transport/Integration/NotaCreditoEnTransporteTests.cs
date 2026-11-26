using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Integration;
using IngenIA365ERP.Application.Transport.Notifications;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Integration;

/// <summary>
/// 2026-11-25 (pedido del dueño: anular y volver a facturar): una nota crédito sobre una factura de servicios llega a Transporte y recalcula
/// sus cargos desde la factura y sus notas confirmadas. La nota de anulación total anula el cargo abierto sin pagos ni aviso (el vehículo se
/// vuelve a facturar en el período); con aviso, lo deja para revisar; una nota parcial sólo baja el valor; repetir el mensaje no cambia nada.
/// Una factura acreditada del todo ya no es «la factura del período»: la facturación emite otra.
/// </summary>
public class NotaCreditoEnTransporteTests
{
    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 12, 5);
    private const decimal Cuota = 73_425m;

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IConsultaDeObligaciones _cartera = Substitute.For<IConsultaDeObligaciones>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private InventoryDocument _factura = null!;
    private ServiceCharge _cargo = null!;

    public NotaCreditoEnTransporteTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(Hoy);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(
            ActorKind.Process, null, null, null, "Proceso de integración", null, ExecutionChannel.Process, "Tarea:integracion", null, null));
        _cartera.PorPagoDeVentaAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<ObligacionDeVentaDto>());
    }

    private async Task SembrarAsync()
    {
        var dueño = new Person { TaxId = "1234567", FirstName = "Ana", LastName = "Prueba" };
        _db.People.Add(dueño);
        await _db.SaveChangesAsync();
        var servicio = new TransportService { ProductCode = "SERV01009", Name = "Administración", Priority = 1 };
        var vehiculo = Vehicle.Registrar(dueño, Hoy.AddYears(-1));
        (vehiculo.Plate, vehiculo.InternalNumber) = ("ABC123", "0009");

        _factura = new InventoryDocument
        {
            Class = DocumentClass.SalesInvoice, DocumentTypeId = 1, Prefix = "FE", Number = 1, OperationDate = Hoy, Currency = "COP", BranchId = 1,
            ExternalReference = "TRN:prueba:202612",
        };
        _factura.Lines.Add(new InventoryDocumentLine
        {
            Document = _factura, LineNumber = 1, ProductId = 1, UnitId = 1, Quantity = 1m, UnitPrice = Cuota, GrossAmount = Cuota, NetAmount = Cuota,
        });
        _factura.Confirmar(1, Ahora);
        _db.InventoryDocuments.Add(_factura);
        await _db.SaveChangesAsync();
        var pago = new DocumentPayment
        {
            DocumentId = _factura.Id, LineNumber = 1, PaymentMeansId = 1, Amount = Cuota, MeansCode = "CREDASO", MeansName = "Crédito asociado",
            MeansClass = Domain.Enums.Core.PaymentMeansClass.AssociateCredit, DianPaymentMeansCode = "1",
        };
        _db.DocumentPayments.Add(pago);

        _cargo = new ServiceCharge
        {
            Vehicle = vehiculo, Service = servicio, Period = "202612", OwnerPersonId = dueño.Id, InvoicePublicId = _factura.PublicId, InvoiceNumber = "FE1",
            InvoiceLinePublicId = _factura.Lines.Single().PublicId, PaymentPublicId = pago.PublicId, Amount = Cuota, Route = ChargeRoute.Lending,
            BillingRun = new BillingRun { Period = "202612" },
        };
        _db.ServiceCharges.Add(_cargo);
        await _db.SaveChangesAsync();
    }

    /// <summary>Una nota crédito confirmada sobre la línea de la factura, por el neto dado.</summary>
    private async Task<InventoryDocument> NotaAsync(decimal neto, bool total)
    {
        var nota = new InventoryDocument
        {
            Class = DocumentClass.CreditNote, DocumentTypeId = 2, Prefix = "NC", Number = 1, OperationDate = Hoy, Currency = "COP", BranchId = 1,
            IsFullReversal = total,
        };
        nota.Lines.Add(new InventoryDocumentLine
        {
            Document = nota, LineNumber = 1, ProductId = 1, UnitId = 1, Quantity = 1m, UnitPrice = neto, GrossAmount = neto, NetAmount = neto,
        });
        nota.Confirmar(1, Ahora);
        _db.InventoryDocuments.Add(nota);
        await _db.SaveChangesAsync();
        var vinculo = new DocumentLink { SourceDocumentId = _factura.Id, TargetDocumentId = nota.Id, Kind = DocumentLinkKind.NoteOf };
        vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = _factura.Lines.Single().Id, TargetLineId = nota.Lines.Single().Id });
        _db.DocumentLinks.Add(vinculo);
        await _db.SaveChangesAsync();
        return nota;
    }

    private ApplyChargeAllocationsCommandHandler Handler() => new(
        _db, _entrantes,
        new EmisionDeSolicitudDeConector(_db, new EmisorDeMensajes(_db, _actor, _reloj), new FacturasDeServicios(_db), _reloj,
            NullLogger<EmisionDeSolicitudDeConector>.Instance),
        _reloj, _cartera);

    private Guid Recibir(InventoryDocument nota, bool total)
    {
        var id = Guid.NewGuid();
        var contenido = new NotaCreditoEmitidaV1 { IsTotalVoid = total };
        var json = System.Text.Json.JsonSerializer.Serialize(contenido);
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = id, Type = NotaCreditoEmitidaV1.Type, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "INV",
            Origin = new MessageOriginV1 { PublicId = nota.PublicId, DocumentClass = DocumentClass.CreditNote, Number = "NC1" },
            Related = new DocumentRefV1 { PublicId = _factura.PublicId, DocumentClass = DocumentClass.SalesInvoice, Number = "FE1" },
        };
        var mensaje = new MensajeEntrante(sobre, contenido, json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), null,
            new EntregaEntrante(IntegrationDestinations.Transport, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null));
        _entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(x => x.Contains(id)), IntegrationDestinations.Transport, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>([mensaje]));
        return id;
    }

    private async Task AplicarAsync(Guid mensaje) =>
        (await Handler().Handle(new ApplyChargeAllocationsCommand([mensaje]), CancellationToken.None)).IsSuccess.Should().BeTrue();

    [Fact]
    public void Transporte_acepta_la_nota_credito()
    {
        new DestinoTransporte(Substitute.For<MediatR.ISender>(), NullLogger<DestinoTransporte>.Instance).Acepta(NotaCreditoEmitidaV1.Type, 1).Should().BeTrue();
    }

    [Fact]
    public async Task La_nota_de_anulacion_total_anula_el_cargo_abierto_y_el_vehiculo_se_vuelve_a_facturar()
    {
        await SembrarAsync();
        var nota = await NotaAsync(Cuota, total: true);

        await AplicarAsync(Recibir(nota, total: true));

        _cargo.Status.Should().Be(ChargeStatus.Voided);
        (await new FacturasDeServicios(_db).PorReferenciaAsync("TRN:prueba:202612", default)).Should()
            .BeNull("una factura acreditada del todo ya no es la factura del período: la facturación emite otra");
    }

    [Fact]
    public async Task Con_aviso_al_sistema_externo_no_se_anula_y_queda_para_revisar()
    {
        await SembrarAsync();
        _cargo.NotifiedRequestPublicId = Guid.NewGuid();
        await _db.SaveChangesAsync();
        var nota = await NotaAsync(Cuota, total: true);

        await AplicarAsync(Recibir(nota, total: true));

        _cargo.Status.Should().Be(ChargeStatus.Open);
        _cargo.ReviewFlag.Should().Be(ChargeReviewFlag.PaymentVoidedAfterNotice);
    }

    [Fact]
    public async Task Una_nota_parcial_solo_baja_el_valor_y_repetirla_no_cambia_nada()
    {
        await SembrarAsync();
        var nota = await NotaAsync(23_425m, total: false);

        await AplicarAsync(Recibir(nota, total: false));
        await AplicarAsync(Recibir(nota, total: false));

        _cargo.Status.Should().Be(ChargeStatus.Open);
        _cargo.Amount.Should().Be(50_000m);
        (await new FacturasDeServicios(_db).PorReferenciaAsync("TRN:prueba:202612", default)).Should()
            .NotBeNull("con una nota parcial la factura sigue siendo la del período");
    }

    [Fact]
    public async Task Notas_parciales_que_suman_toda_la_linea_anulan_el_cargo()
    {
        await SembrarAsync();
        await NotaAsync(23_425m, total: false);
        var segunda = await NotaAsync(50_000m, total: false);

        await AplicarAsync(Recibir(segunda, total: false));

        _cargo.Status.Should().Be(ChargeStatus.Voided);
        (await new FacturasDeServicios(_db).PorReferenciaAsync("TRN:prueba:202612", default)).Should().BeNull();
    }

    [Theory]
    [InlineData(73_425, 73_425, 0, 73_425)]
    [InlineData(73_425, 73_425, 23_425, 50_000)]
    [InlineData(73_425, 73_425, 73_425, 0)]
    [InlineData(73_425, 73_425, 80_000, 0)]
    [InlineData(70_000, 73_425, 23_425, 47_667.69)]
    [InlineData(70_000, 0, 0, 0)]
    public void El_valor_despues_de_notas_es_proporcional_a_lo_que_queda_de_la_linea(decimal original, decimal peso, decimal acreditado, decimal esperado)
    {
        ApplyChargeAllocationsCommandHandler.ValorDespuesDeNotas(original, peso, acreditado).Should().Be(esperado);
    }
}
