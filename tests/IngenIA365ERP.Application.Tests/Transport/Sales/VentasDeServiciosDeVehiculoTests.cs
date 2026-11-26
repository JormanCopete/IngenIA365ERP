using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Notifications;
using IngenIA365ERP.Application.Transport.Sales;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Sales;

/// <summary>
/// 2026-11-18 (feature 020, pedido del dueño): la placa o el lateral del vehículo en la línea de venta de un servicio. Piden referencia los
/// productos de los servicios activos; lo digitado se busca como placa, lateral o número interno entre los no retirados y se guarda la placa.
/// </summary>
public class VentasDeServiciosDeVehiculoTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 18);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Guid _radio = Guid.NewGuid();

    public VentasDeServiciosDeVehiculoTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc));
    }

    private VentasDeServiciosDeVehiculo Puerto() => new(_db, new FacturasDeServicios(_db),
        new EmisionDeSolicitudDeConector(_db, new EmisorDeMensajes(_db, Substitute.For<IActorActual>(), _reloj), new FacturasDeServicios(_db), _reloj,
            NullLogger<EmisionDeSolicitudDeConector>.Instance),
        _reloj);

    private async Task<Person> SembrarAsync()
    {
        var dueno = new Person { TaxId = "1234567", FirstName = "Ana", LastName = "Prueba" };
        _db.People.Add(dueno);
        _db.TransportServices.Add(new TransportService { ProductPublicId = _radio, ProductCode = "RADIO", Name = "Radio", Priority = 1 });
        _db.TransportServices.Add(new TransportService { ProductPublicId = Guid.NewGuid(), ProductCode = "VIEJO", Name = "Viejo", IsActive = false });
        Vehiculo(dueno, "ABC123", "0101", "101");
        Vehiculo(dueno, "XYZ987", "0202", "202");
        var retirado = Vehiculo(dueno, "RET111", "0303", "303");
        retirado.Retirar(Hoy.AddDays(-10), "Venta del vehículo");
        // El lateral de uno igual al número interno de otro: manda el lateral.
        Vehiculo(dueno, "AMB111", "0404", "555");
        Vehiculo(dueno, "AMB222", "555", "0505");
        await _db.SaveChangesAsync();
        return dueno;
    }

    private Vehicle Vehiculo(Person dueno, string placa, string interno, string lateral)
    {
        var v = Vehicle.Registrar(dueno, Hoy.AddYears(-1));
        v.Plate = placa;
        v.InternalNumber = interno;
        v.RadioCode = lateral;
        _db.Vehicles.Add(v);
        return v;
    }

    [Fact]
    public async Task Piden_referencia_solo_los_productos_de_los_servicios_activos()
    {
        await SembrarAsync();

        var piden = await Puerto().ProductosQueLaPidenAsync(CancellationToken.None);

        piden.Should().ContainSingle().Which.Should().Be(new ProductoConReferencia(_radio, VentasDeServiciosDeVehiculo.Rotulo, VentasDeServiciosDeVehiculo.RotuloDeLaPlacaExterna));
    }

    [Theory]
    [InlineData("abc-123", "ABC123")]
    [InlineData("101", "ABC123")]
    [InlineData("0202", "XYZ987")]
    [InlineData(" xyz 987 ", "XYZ987")]
    public async Task Lo_digitado_se_busca_como_placa_lateral_o_numero_interno_y_se_guarda_la_placa(string digitado, string placa)
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, digitado, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Valor.Should().Be(placa);
        r.Value.Etiqueta.Should().StartWith(placa).And.Contain("Ana");
    }

    [Fact]
    public async Task El_lateral_manda_sobre_el_numero_interno_de_otro_vehiculo()
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, "555", CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valor.Should().Be("AMB111");
    }

    [Theory]
    [InlineData("RET111")]
    [InlineData("NOEXISTE")]
    [InlineData("12345678901")]
    public async Task Una_placa_que_no_esta_en_el_parque_no_sirve(string digitado)
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, digitado, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Transport.Sale.VehicleNotFound");
    }

    /// <summary>2026-11-25: los servicios también se venden a vehículos que no son de la cooperativa; se identifican por el lateral (números).</summary>
    [Theory]
    [InlineData("618", "618")]
    [InlineData(" 7 ", "7")]
    [InlineData("303", "303")] // el lateral de un vehículo retirado ya no es del parque
    [InlineData("1234567890", "1234567890")]
    public async Task Un_numero_fuera_del_parque_es_el_lateral_de_un_vehiculo_externo(string digitado, string guardado)
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, digitado, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valor.Should().Be(guardado);
        r.Value.Etiqueta.Should().Contain("fuera del parque");
        // 2026-11-26: el lateral solo no basta: el externo pide también su placa.
        r.Value.FaltaComplemento.Should().Be(VentasDeServiciosDeVehiculo.RotuloDeLaPlacaExterna);
    }

    /// <summary>2026-11-26 (pedido del dueño): el externo se vende con lateral y placa; la línea guarda «lateral/placa» normalizado.</summary>
    [Theory]
    [InlineData("618/wdk-637", "618/WDK637", "WDK637")]
    [InlineData(" 7 / abc 12d ", "7/ABC12D", "ABC12D")]
    [InlineData("303/RET111", "303/RET111", "RET111")] // la placa de un retirado ya no es del parque
    public async Task El_lateral_externo_con_su_placa_queda_completo(string digitado, string guardado, string placa)
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, digitado, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Valor.Should().Be(guardado);
        r.Value.FaltaComplemento.Should().BeNull();
        r.Value.Etiqueta.Should().Contain(placa).And.Contain("fuera del parque");
    }

    [Theory]
    [InlineData("618/AB", "Transport.Sale.ExternalPlateInvalid")]
    [InlineData("618/ABC#12", "Transport.Sale.ExternalPlateInvalid")]
    [InlineData("618/abc-123", "Transport.Sale.ExternalPlateInFleet")] // la placa es de un vehículo del parque
    [InlineData("101/ZZZ999", "Transport.Sale.FleetVehicleNoExternalPlate")] // el lateral es de un vehículo del parque
    [InlineData("ABC123/ZZZ999", "Transport.Sale.FleetVehicleNoExternalPlate")]
    [InlineData("NOEXISTE/ZZZ999", "Transport.Sale.VehicleNotFound")]
    public async Task La_placa_del_externo_se_valida(string digitado, string codigo)
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, digitado, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(codigo);
    }

    /// <summary>
    /// El aviso del externo lleva lateral y placa; el cargo externo anterior al 2026-11-26 (sin placa) sigue con la placa vacía, que
    /// <c>DestinoConectores.Valores</c> llena con el lateral al enviar.
    /// </summary>
    [Theory]
    [InlineData("WDK637", "WDK637")]
    [InlineData(null, "")]
    public async Task El_cargo_de_un_vehiculo_externo_avisa_con_el_lateral_y_la_placa(string? placaExterna, string placaAvisada)
    {
        _db.Branches.Add(new Branch { Name = "Principal" });
        var conector = new Connector { Code = "RADIO", Name = "Radio", IsEnabled = true, CurrentVersionNumber = 1 };
        conector.Versions.Add(new ConnectorVersion { Number = 1, BaseUrl = "https://proveedor.example.com", DefinitionJson = "{}", DefinitionSha256 = "x" });
        var cargo = new ServiceCharge
        {
            VehicleReference = "618", ExternalPlate = placaExterna, Service = new TransportService { ProductCode = "RADIO", Name = "Radio", Priority = 1, Connector = conector },
            Period = "202611", Origin = ChargeOrigin.Sale, InvoicePublicId = Guid.NewGuid(), InvoiceNumber = "FV-1", InvoiceLinePublicId = Guid.NewGuid(),
            Amount = 40_000m, Route = ChargeRoute.Cash, Status = ChargeStatus.Paid,
        };
        _db.ServiceCharges.Add(cargo);
        await _db.SaveChangesAsync();
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new IngenIA365ERP.Application.Common.Execution.Actor(
            Domain.Enums.Integration.ActorKind.Process, null, null, null, "Proceso de integración", null,
            Domain.Enums.Integration.ExecutionChannel.Process, "Tarea:integracion", null, null));
        var emision = new EmisionDeSolicitudDeConector(_db, new EmisorDeMensajes(_db, actor, _reloj), new FacturasDeServicios(_db),
            _reloj, NullLogger<EmisionDeSolicitudDeConector>.Instance);

        var envio = await emision.EmitirAsync(cargo, new PagoQueSalda("FV-1", Hoy), CancellationToken.None);
        await _db.SaveChangesAsync();

        envio.Should().NotBeNull();
        var carga = _db.IntegrationMessages.Should().ContainSingle().Subject.PayloadJson;
        carga.Should().Contain("\"vehiculo.radioMovil\":\"618\"").And.Contain($"\"vehiculo.placa\":\"{placaAvisada}\"");
    }

    /// <summary>El POS (documento equivalente) valida y confirma por el mismo camino que la factura: el externo pasa y deja su cargo.</summary>
    [Theory]
    [InlineData(DocumentClass.PosEquivalentDocument, "618/wdk637", "618/WDK637", "618")]
    [InlineData(DocumentClass.PosEquivalentDocument, "101", "ABC123", null)]
    [InlineData(DocumentClass.SalesInvoice, "618/WDK637", "618/WDK637", "618")]
    public async Task La_venta_del_POS_y_de_la_factura_acepta_vehiculos_externos(DocumentClass clase, string digitado, string guardado, string? externo)
    {
        var venta = await VentaAsync(clase, digitado);

        var falta = await ReferenciaDeLinea.ValidarAsync(_db, [Puerto()], venta, CancellationToken.None);
        falta.Should().BeNull();
        venta.Lines.Single().ItemReference.Should().Be(guardado);
        await _db.SaveChangesAsync();

        var r = await Puerto().AlConfirmarVentaAsync(venta.PublicId, CancellationToken.None);
        await _db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var cargo = _db.ServiceCharges.Should().ContainSingle().Subject;
        cargo.VehicleReference.Should().Be(externo);
        cargo.ExternalPlate.Should().Be(externo is null ? null : "WDK637");
        (cargo.VehicleId is null).Should().Be(externo is not null);
        cargo.Status.Should().Be(ChargeStatus.Paid);
    }

    /// <summary>2026-11-26: el externo sin placa no se confirma, ni en el POS ni en la factura.</summary>
    [Theory]
    [InlineData(DocumentClass.PosEquivalentDocument)]
    [InlineData(DocumentClass.SalesInvoice)]
    public async Task La_venta_a_un_externo_sin_placa_no_se_confirma(DocumentClass clase)
    {
        var venta = await VentaAsync(clase, "618");

        var falta = await ReferenciaDeLinea.ValidarAsync(_db, [Puerto()], venta, CancellationToken.None);

        falta.Should().NotBeNull();
        falta!.Code.Should().Be("Inventory.Line.ReferenceComplementRequired");
        falta.Message.Should().Contain("placa del vehículo externo").And.Contain("lateral 618");
    }

    private async Task<InventoryDocument> VentaAsync(DocumentClass clase, string digitado)
    {
        await SembrarAsync();
        var producto = new Product { Code = "RADIO", Name = "Radio", CategoryId = 1, BaseUnitId = 1, PublicId = _radio };
        _db.Products.Add(producto);
        await _db.SaveChangesAsync();
        var venta = new InventoryDocument { Class = clase, DocumentTypeId = 1, Prefix = "POS", Number = 1, OperationDate = Hoy, Currency = "COP", BranchId = 1 };
        venta.Lines.Add(new InventoryDocumentLine
        {
            Document = venta, LineNumber = 1, ProductId = producto.Id, UnitId = 1, Quantity = 1m, UnitPrice = 40_000m, GrossAmount = 40_000m,
            NetAmount = 40_000m, ItemReference = digitado,
        });
        _db.InventoryDocuments.Add(venta);
        await _db.SaveChangesAsync();
        _db.DocumentPayments.Add(new DocumentPayment
        {
            DocumentId = venta.Id, LineNumber = 1, PaymentMeansId = 1, Amount = 40_000m, MeansCode = "EFECTIVO", MeansName = "Efectivo",
            MeansClass = Domain.Enums.Core.PaymentMeansClass.Cash, DianPaymentMeansCode = "10",
        });
        return venta;
    }
}
