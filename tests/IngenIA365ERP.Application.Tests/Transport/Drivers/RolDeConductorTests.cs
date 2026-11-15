using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Drivers;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Drivers;

/// <summary>
/// Feature 020 (T094; US5; R5; FR-018, FR-019): la marca de conductor la da el registro; un vehículo admite varios conductores y a lo sumo
/// un titular vigente; el mismo conductor no se asigna dos veces con vigencias cruzadas; el propietario puede manejar su vehículo.
/// </summary>
public class RolDeConductorTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 20);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public RolDeConductorTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc));
    }

    private Person Persona(string doc)
    {
        var p = new Person { FirstName = "Conductor", LastName = doc, TaxId = doc, Username = doc };
        _db.People.Add(p);
        _db.SaveChanges();
        return p;
    }

    private async Task<Vehicle> VehiculoAsync(string placa, Person dueño)
    {
        var v = (await new VehicleRegistrar(_db, _reloj).RegistrarAsync(new VehicleInput { Plate = placa, InternalNumber = placa[..4] }, dueño, ChangeSource.Screen, default)).Value;
        await _db.SaveChangesAsync();
        return v;
    }

    [Fact]
    public async Task Registrar_marca_a_la_persona_y_no_registra_dos_veces()
    {
        var persona = Persona("10");
        var rol = new RolDeConductor(_db, _reloj);

        (await rol.AsignarAsync(persona, Hoy, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();

        persona.IsDriver.Should().BeTrue();
        (await rol.AsignarAsync(persona, Hoy, default)).Error.Code.Should().Be("Driver.AlreadyRegistered");
    }

    [Fact]
    public async Task Un_vehiculo_admite_varios_conductores_y_un_solo_titular()
    {
        var dueño = Persona("20");
        var v = await VehiculoAsync("GGG777", dueño);
        var rol = new RolDeConductor(_db, _reloj);
        var a = (await rol.AsignarAsync(Persona("21"), Hoy, default)).Value;
        var b = (await rol.AsignarAsync(Persona("22"), Hoy, default)).Value;
        await _db.SaveChangesAsync();

        (await rol.AsignarVehiculoAsync(a, v, Hoy, titular: true, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();
        (await rol.AsignarVehiculoAsync(b, v, Hoy, titular: true, default)).Error.Code.Should().Be("Driver.PrimaryAlreadyExists");
        (await rol.AsignarVehiculoAsync(b, v, Hoy, titular: false, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();
        (await rol.AsignarVehiculoAsync(a, v, Hoy.AddDays(1), titular: false, default)).Error.Code.Should().Be("Driver.AlreadyAssigned");
    }

    [Fact]
    public async Task El_propietario_puede_manejar_su_vehiculo_y_al_retirarlo_pierde_la_marca()
    {
        var dueño = Persona("30");
        var v = await VehiculoAsync("HHH888", dueño);
        var rol = new RolDeConductor(_db, _reloj);
        var conductor = (await rol.AsignarAsync(dueño, Hoy, default)).Value;
        await _db.SaveChangesAsync();

        (await rol.AsignarVehiculoAsync(conductor, v, Hoy, titular: true, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();
        (await rol.RetirarAsync(conductor, Hoy.AddDays(5), default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();

        dueño.IsDriver.Should().BeFalse();
        _db.DriverAssignments.Single().To.Should().Be(Hoy.AddDays(5));
    }
}
