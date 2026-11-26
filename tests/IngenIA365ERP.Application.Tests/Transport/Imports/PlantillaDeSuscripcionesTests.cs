using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Imports;
using IngenIA365ERP.Application.Transport.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeSuscripciones;

namespace IngenIA365ERP.Application.Tests.Transport.Imports;

/// <summary>
/// 2026-11-25 (pedido del dueño): la plantilla de suscripciones también termina en lote. Una fila con <c>hasta</c> sobre una suscripción que
/// existe (la que se cruza con sus fechas: <c>desde</c> sólo la identifica) la termina en esa fecha por <see cref="SuscripcionDeServicio.Terminar"/>
/// y se informa como «actualizar» con el campo; «conservar» no la toca; la misma fecha no cambia nada; con precio en la misma fila, termina y
/// pacta.
/// </summary>
public class PlantillaDeSuscripcionesTests
{
    private static readonly string[] Encabezados = [P.PlacaCol, P.Producto, P.Desde, P.Hasta, P.Precio];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private TablaLeida _tabla = new([], [], "xlsx");
    private VehicleSubscription _existente = null!;

    public PlantillaDeSuscripcionesTests()
    {
        _reloj.HoyLocal.Returns(new DateOnly(2026, 11, 25));
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc));
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(["Transport.Services.Manage"]));
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(["Datos"])));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));

        var dueño = new Person { IdType = "CC", TaxId = "1010101010", FirstName = "Ana", LastName = "Propietaria", CreatedBy = "test" };
        _db.People.Add(dueño);
        _db.SaveChanges();
        var v = Vehicle.Registrar(dueño, new DateOnly(2026, 1, 1));
        (v.Plate, v.InternalNumber) = ("WDK622", "622");
        _db.Vehicles.Add(v);
        var s = new TransportService { ProductPublicId = Guid.NewGuid(), ProductCode = "SERV01009", Name = "Administración", Priority = 1 };
        _db.TransportServices.Add(s);
        _db.SaveChanges();
        _existente = new VehicleSubscription { VehicleId = v.Id, ServiceId = s.Id, From = new DateOnly(2026, 11, 25) };
        _existente.Prices.Add(new SubscriptionPrice { Subscription = _existente, Price = 73_425m, From = new DateOnly(2026, 11, 25) });
        _db.VehicleSubscriptions.Add(_existente);
        _db.SaveChanges();
    }

    private void Filas(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private async Task<ImportResultDto> CorrerAsync(ModoDeImportacion modo, SiYaExiste existentes = SiYaExiste.Actualizar)
    {
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos,
            new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());
        var handler = new ImportSubscriptionsCommandHandler(_db, ejecutor, new SuscripcionDeServicio(_db, _reloj));
        var r = await handler.Handle(new ImportSubscriptionsCommand(modo, new ArchivoDeImportacion("suscripciones.xlsx", [1, 2, 3]), "Retiro de la administración",
            existentes) { OperationKey = Guid.NewGuid() }, CancellationToken.None);
        if (r.IsSuccess) return r.Value;
        r.Error.Should().BeOfType<ErrorConDatos>($"un fallo sin resultado: {r.Error.Code} {r.Error.Message}");
        return ((ErrorConDatos)r.Error).Data.Should().BeOfType<ImportResultDto>().Subject;
    }

    private async Task<VehicleSubscription> GuardadaAsync() =>
        await _db.VehicleSubscriptions.Include(s => s.Prices).AsNoTracking().SingleAsync(s => s.Id == _existente.Id);

    [Fact]
    public async Task Una_fila_con_hasta_termina_la_suscripcion_vigente_y_lo_informa_como_actualizacion()
    {
        Filas(["WDK622", "SERV01009", "2026-12-01", "2026-12-31", null]);

        var r = await CorrerAsync(ModoDeImportacion.Apply);

        r.Valid.Should().BeTrue(string.Join("; ", r.Errors.Select(e => e.Message)));
        r.Sheets.Single().Updated.Should().Be(1);
        r.Sheets.Single().Created.Should().Be(0, "la fecha «desde» sólo identifica la suscripción");
        var s = await GuardadaAsync();
        s.To.Should().Be(new DateOnly(2026, 12, 31));
        s.Reason.Should().Contain("Terminada por plantilla");
        s.Prices.Single().To.Should().Be(new DateOnly(2026, 12, 31));
        _db.VehicleSubscriptions.Count().Should().Be(1);
    }

    [Fact]
    public async Task Revisar_dice_que_terminaria_y_no_guarda()
    {
        Filas(["WDK622", "SERV01009", "2026-11-25", "2026-12-31", null]);

        var r = await CorrerAsync(ModoDeImportacion.Review);

        r.Applied.Should().BeFalse();
        r.Sheets.Single().Updated.Should().Be(1);
        (await GuardadaAsync()).To.Should().BeNull();
    }

    [Fact]
    public async Task Conservar_no_termina()
    {
        Filas(["WDK622", "SERV01009", "2026-11-25", "2026-12-31", null]);

        var r = await CorrerAsync(ModoDeImportacion.Apply, SiYaExiste.Conservar);

        r.Sheets.Single().Updated.Should().Be(0);
        (await GuardadaAsync()).To.Should().BeNull();
    }

    [Fact]
    public async Task La_misma_fecha_de_fin_no_cambia_nada_y_otra_la_mueve()
    {
        _existente.To = new DateOnly(2026, 12, 31);
        _db.SaveChanges();

        Filas(["WDK622", "SERV01009", "2026-11-25", "2026-12-31", null]);
        (await CorrerAsync(ModoDeImportacion.Apply)).Sheets.Single().Updated.Should().Be(0);

        Filas(["WDK622", "SERV01009", "2026-11-25", "2027-01-31", null]);
        (await CorrerAsync(ModoDeImportacion.Apply)).Sheets.Single().Updated.Should().Be(1);
        (await GuardadaAsync()).To.Should().Be(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public async Task Hasta_y_precio_en_la_misma_fila_terminan_y_pactan()
    {
        Filas(["WDK622", "SERV01009", "2026-12-01", "2027-06-30", "80000"]);

        var r = await CorrerAsync(ModoDeImportacion.Apply);

        r.Valid.Should().BeTrue(string.Join("; ", r.Errors.Select(e => e.Message)));
        var s = await GuardadaAsync();
        s.To.Should().Be(new DateOnly(2027, 6, 30));
        s.Prices.OrderBy(p => p.From).Select(p => (p.Price, p.From, p.To)).Should().Equal(
            (73_425m, new DateOnly(2026, 11, 25), (DateOnly?)new DateOnly(2026, 11, 30)),
            (80_000m, new DateOnly(2026, 12, 1), (DateOnly?)new DateOnly(2027, 6, 30)));
    }
}
