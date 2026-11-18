using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Notifications;
using IngenIA365ERP.Application.Transport.Sales;
using IngenIA365ERP.Domain.Entities.Core;
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

        piden.Should().ContainSingle().Which.Should().Be(new ProductoConReferencia(_radio, VentasDeServiciosDeVehiculo.Rotulo));
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
    [InlineData("303")]
    [InlineData("NOEXISTE")]
    public async Task Un_vehiculo_retirado_o_inexistente_no_sirve(string digitado)
    {
        await SembrarAsync();

        var r = await Puerto().ResolverAsync(_radio, digitado, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Transport.Sale.VehicleNotFound");
    }
}
